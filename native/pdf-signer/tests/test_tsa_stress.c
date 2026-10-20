#define _POSIX_C_SOURCE 200809L
#include "tsa_client.h"

#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#define STRESS_THREAD_COUNT 8
#define STRESS_REQUESTS_PER_THREAD 1000


typedef struct
{
  int status_counts[TSA_STATUS_INTERNAL_ERROR + 1];
} stress_worker_result_t;

static void* stress_worker_main(void* arg) {
  stress_worker_result_t* result = arg;

  unsigned char signature[256];
  memset(signature, 0x42, sizeof(signature));

  tsa_config_t config = {
      .url        = "http://127.0.0.1:8080/",
      .timeout_ms = 5000,
  };

  for (int i = 0; i < STRESS_REQUESTS_PER_THREAD; i++) {
    tsa_result_t tsa_result = {0};

    tsa_status_t status = tsa_request_timestamp(&config, signature, sizeof(signature), &tsa_result);

    if (status != TSA_STATUS_OK) {
      if (status >= TSA_STATUS_OK && status <= TSA_STATUS_INTERNAL_ERROR) {
        result->status_counts[status]++;
      }

      continue;
    }

    if (!tsa_result.token_der || tsa_result.token_der_len == 0) {
      result->status_counts[TSA_STATUS_INTERNAL_ERROR]++;
    }

    tsa_result_dispose(&tsa_result);
  }

  return NULL;
}

static double elapsed_seconds(const struct timespec* start, const struct timespec* end) {
  return (double)(end->tv_sec - start->tv_sec) +
         (double)(end->tv_nsec - start->tv_nsec) / 1000000000.0;
}

int main(void) {
  pthread_t threads[STRESS_THREAD_COUNT];

  stress_worker_result_t results[STRESS_THREAD_COUNT];
  memset(results, 0, sizeof(results));

  struct timespec start;
  struct timespec end;

  clock_gettime(CLOCK_MONOTONIC, &start);

  for (int i = 0; i < STRESS_THREAD_COUNT; i++) {
    if (pthread_create(&threads[i], NULL, stress_worker_main, &results[i]) != 0) {
      fprintf(stderr, "Failed to create stress thread %d\n", i);
      return 1;
    }
  }

  for (int i = 0; i < STRESS_THREAD_COUNT; i++) {
    pthread_join(threads[i], NULL);
  }

  clock_gettime(CLOCK_MONOTONIC, &end);

  int status_counts[TSA_STATUS_INTERNAL_ERROR + 1] = {0};

  for (int i = 0; i < STRESS_THREAD_COUNT; i++) {
    for (int status = TSA_STATUS_OK; status <= TSA_STATUS_INTERNAL_ERROR; status++) {
      status_counts[status] += results[i].status_counts[status];
    }
  }

  int failed = 0;

  for (int status = TSA_STATUS_INVALID_ARGUMENT; status <= TSA_STATUS_INTERNAL_ERROR; status++) {
    failed += status_counts[status];
  }

  printf("Failures: %d\n", failed);

  printf("INVALID_ARGUMENT: %d\n", status_counts[TSA_STATUS_INVALID_ARGUMENT]);

  printf("REQUEST_ERROR: %d\n", status_counts[TSA_STATUS_REQUEST_ERROR]);

  printf("NETWORK_ERROR: %d\n", status_counts[TSA_STATUS_NETWORK_ERROR]);

  printf("RESPONSE_ERROR: %d\n", status_counts[TSA_STATUS_RESPONSE_ERROR]);

  printf("VERIFY_ERROR: %d\n", status_counts[TSA_STATUS_VERIFY_ERROR]);

  printf("INTERNAL_ERROR: %d\n", status_counts[TSA_STATUS_INTERNAL_ERROR]);


  int total_requests = STRESS_THREAD_COUNT * STRESS_REQUESTS_PER_THREAD;

  double seconds = elapsed_seconds(&start, &end);

  double requests_per_second = (double)total_requests / seconds;

  printf("TSA stress test\n");
  printf("Requests: %d\n", total_requests);
  printf("Failures: %d\n", failed);
  printf("Time: %.2f seconds\n", seconds);
  printf("Throughput: %.2f req/s\n", requests_per_second);

  if (failed != 0) {
    fprintf(stderr, "TSA stress test failed\n");
    return 1;
  }

  printf("TSA stress test passed\n");

  return 0;
}
