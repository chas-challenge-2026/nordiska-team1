// ==============================================================================
// Nordiska TSA Benchmark Latency & Jitter Proxy
// External proxy to inject randomized network latency (e.g. 2-20ms) into TSA calls
// Zero modification to project/production source code.
// ==============================================================================
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <pthread.h>
#include <stdint.h>
#include <time.h>
#include <signal.h>
#include <arpa/inet.h>
#include <sys/socket.h>
#include <netinet/in.h>

static int g_min_ms = 2;
static int g_max_ms = 20;
static int g_target_port = 8081;
static int g_listen_port = 8082;
static volatile sig_atomic_t g_running = 1;

static void handle_sig(int s) {
    (void)s;
    g_running = 0;
}

static uint64_t random_delay_ms(void) {
    static __thread uint64_t prng_state = 0;
    if (prng_state == 0) {
        struct timespec ts;
        clock_gettime(CLOCK_MONOTONIC, &ts);
        prng_state = (uint64_t)ts.tv_nsec ^ ((uint64_t)(uintptr_t)pthread_self() << 16);
        if (prng_state == 0) prng_state = 0xdeadbeefcafebabeULL;
    }

    prng_state ^= prng_state << 13;
    prng_state ^= prng_state >> 7;
    prng_state ^= prng_state << 17;

    int range = (g_max_ms - g_min_ms) + 1;
    if (range <= 1) return (uint64_t)g_min_ms;

    return (uint64_t)(g_min_ms + (int)(prng_state % (uint64_t)range));
}

typedef struct {
    int client_fd;
} proxy_conn_t;

static void* connection_worker(void* arg) {
    proxy_conn_t* conn = (proxy_conn_t*)arg;
    int c_fd = conn->client_fd;
    free(conn);

    // 1. Connect to backend TSA on 127.0.0.1:g_target_port
    int s_fd = socket(AF_INET, SOCK_STREAM, 0);
    if (s_fd < 0) {
        close(c_fd);
        return NULL;
    }

    struct sockaddr_in s_addr;
    memset(&s_addr, 0, sizeof(s_addr));
    s_addr.sin_family = AF_INET;
    s_addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    s_addr.sin_port = htons(g_target_port);

    if (connect(s_fd, (struct sockaddr*)&s_addr, sizeof(s_addr)) != 0) {
        close(s_fd);
        close(c_fd);
        return NULL;
    }

    // Set 5-second socket timeout
    struct timeval tv = {.tv_sec = 5, .tv_usec = 0};
    setsockopt(c_fd, SOL_SOCKET, SO_RCVTIMEO, &tv, sizeof(tv));
    setsockopt(s_fd, SOL_SOCKET, SO_RCVTIMEO, &tv, sizeof(tv));

    // 2. Read full request from client
    char buffer[16384];
    ssize_t n_read = recv(c_fd, buffer, sizeof(buffer), 0);
    if (n_read <= 0) {
        close(s_fd);
        close(c_fd);
        return NULL;
    }

    // 3. Inject simulated network transit latency with jitter
    uint64_t delay_ms = random_delay_ms();
    if (delay_ms > 0) {
        usleep((useconds_t)delay_ms * 1000);
    }

    // 4. Send request to backend TSA server
    ssize_t sent = 0;
    while (sent < n_read) {
        ssize_t n = send(s_fd, buffer + sent, n_read - sent, MSG_NOSIGNAL);
        if (n <= 0) break;
        sent += n;
    }

    // 5. Read response from backend TSA server & forward to client
    while (1) {
        ssize_t n = recv(s_fd, buffer, sizeof(buffer), 0);
        if (n <= 0) break;
        ssize_t c_sent = 0;
        while (c_sent < n) {
            ssize_t wn = send(c_fd, buffer + c_sent, n - c_sent, MSG_NOSIGNAL);
            if (wn <= 0) break;
            c_sent += wn;
        }
    }

    close(s_fd);
    close(c_fd);
    return NULL;
}

int main(int argc, char** argv) {
    if (argc > 1) g_min_ms = atoi(argv[1]);
    if (argc > 2) g_max_ms = atoi(argv[2]);
    if (argc > 3) g_listen_port = atoi(argv[3]);
    if (argc > 4) g_target_port = atoi(argv[4]);

    const char* min_env = getenv("TSA_LATENCY_MIN_MS");
    const char* max_env = getenv("TSA_LATENCY_MAX_MS");
    if (min_env && *min_env) g_min_ms = atoi(min_env);
    if (max_env && *max_env) g_max_ms = atoi(max_env);

    const char* base_env = getenv("TSA_LATENCY_MS");
    const char* pct_env = getenv("TSA_JITTER_PERCENT");
    if (base_env && *base_env) {
        int base = atoi(base_env);
        int pct = (pct_env && *pct_env) ? atoi(pct_env) : 50;
        int jitter = (base * pct) / 100;
        g_min_ms = base - jitter;
        g_max_ms = base + jitter;
    }

    if (g_min_ms < 0) g_min_ms = 0;
    if (g_max_ms < g_min_ms) g_max_ms = g_min_ms;

    signal(SIGINT, handle_sig);
    signal(SIGTERM, handle_sig);
    signal(SIGPIPE, SIG_IGN);

    int listen_fd = socket(AF_INET, SOCK_STREAM, 0);
    if (listen_fd < 0) {
        perror("socket");
        return 1;
    }

    int opt = 1;
    setsockopt(listen_fd, SOL_SOCKET, SO_REUSEADDR, &opt, sizeof(opt));

    struct sockaddr_in l_addr;
    memset(&l_addr, 0, sizeof(l_addr));
    l_addr.sin_family = AF_INET;
    l_addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    l_addr.sin_port = htons(g_listen_port);

    if (bind(listen_fd, (struct sockaddr*)&l_addr, sizeof(l_addr)) != 0) {
        perror("bind");
        close(listen_fd);
        return 1;
    }

    if (listen(listen_fd, 128) != 0) {
        perror("listen");
        close(listen_fd);
        return 1;
    }

    printf("TSA Latency Proxy active: 127.0.0.1:%d -> 127.0.0.1:%d (Jitter: %d–%d ms)\n",
           g_listen_port, g_target_port, g_min_ms, g_max_ms);
    fflush(stdout);

    while (g_running) {
        struct sockaddr_in c_addr;
        socklen_t c_len = sizeof(c_addr);
        int c_fd = accept(listen_fd, (struct sockaddr*)&c_addr, &c_len);
        if (c_fd < 0) {
            if (!g_running) break;
            continue;
        }

        proxy_conn_t* conn = malloc(sizeof(proxy_conn_t));
        if (!conn) {
            close(c_fd);
            continue;
        }
        conn->client_fd = c_fd;

        pthread_t th;
        pthread_attr_t attr;
        pthread_attr_init(&attr);
        pthread_attr_setdetachstate(&attr, PTHREAD_CREATE_DETACHED);

        if (pthread_create(&th, &attr, connection_worker, conn) != 0) {
            close(c_fd);
            free(conn);
        }
        pthread_attr_destroy(&attr);
    }

    close(listen_fd);
    printf("TSA Latency Proxy shut down.\n");
    return 0;
}

