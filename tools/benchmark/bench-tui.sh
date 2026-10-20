#!/usr/bin/env bash
# ==============================================================================
# Nordiska Benchmark Environment & Worker TUI
# Interactive switcher for benchmark-relevant environment variables
# ==============================================================================
set -euo pipefail

# Resolve paths
SOURCE_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(git -C "$SOURCE_DIR" rev-parse --show-toplevel 2>/dev/null || (cd -- "$SOURCE_DIR/.." && pwd))"
ENV_FILE="$REPO_ROOT/infra/.env"
COMPOSE_MAIN="$REPO_ROOT/infra/docker-compose.yml"
COMPOSE_OVERRIDE="$REPO_ROOT/infra/docker-compose.override.yml"
NORDISKA_ENV_BIN="$REPO_ROOT/tools/env/nordiska-env"

# Color helpers
if [[ -t 1 && "${TERM:-}" != "dumb" && -z "${NO_COLOR:-}" ]]; then
    C_RESET="\033[0m"
    C_BOLD="\033[1m"
    C_CYAN="\033[36m"
    C_BOLD_CYAN="\033[1;36m"
    C_GREEN="\033[32m"
    C_BOLD_GREEN="\033[1;32m"
    C_YELLOW="\033[33m"
    C_BOLD_YELLOW="\033[1;33m"
    C_RED="\033[31m"
    C_BOLD_RED="\033[1;31m"
    C_GRAY="\033[90m"
else
    C_RESET=""
    C_BOLD=""
    C_CYAN=""
    C_BOLD_CYAN=""
    C_GREEN=""
    C_BOLD_GREEN=""
    C_YELLOW=""
    C_BOLD_YELLOW=""
    C_RED=""
    C_BOLD_RED=""
    C_GRAY=""
fi

STATUS_MSG=""
STATUS_COLOR="$C_GRAY"

set_env_val() {
    local key="$1"
    local val="$2"
    if [[ -x "$NORDISKA_ENV_BIN" ]]; then
        "$NORDISKA_ENV_BIN" --set "${key}=${val}" >/dev/null 2>&1
    else
        if grep -q "^${key}=" "$ENV_FILE" 2>/dev/null; then
            sed -i "s|^${key}=.*|${key}=${val}|" "$ENV_FILE"
        else
            echo "${key}=${val}" >> "$ENV_FILE"
        fi
    fi
}

get_env_val() {
    local key="$1"
    local default_val="${2:-}"
    if [[ -f "$ENV_FILE" ]]; then
        local match
        match=$(grep -E "^${key}=" "$ENV_FILE" | head -n1 | cut -d'=' -f2- || true)
        if [[ -n "$match" ]]; then
            echo "$match"
            return
        fi
    fi
    echo "$default_val"
}

ensure_tsa_proxy() {
    local signing url
    signing=$(get_env_val "NORDISKA_PDF_ENABLE_SIGNING" "0")
    url=$(get_env_val "PDF_SIGNER_TSA_URL" "http://127.0.0.1:8081/")
    if [[ "$signing" == "1" && "$url" == *"8082"* ]]; then
        local min_ms max_ms
        min_ms=$(get_env_val "PDF_SIGNER_TSA_LATENCY_MIN_MS" "2")
        max_ms=$(get_env_val "PDF_SIGNER_TSA_LATENCY_MAX_MS" "20")
        
        docker exec nordiska-v2-pdf-signer-1 pkill tsa_proxy 2>/dev/null || true
        docker cp "$REPO_ROOT/tools/benchmark/tsa_proxy.c" nordiska-v2-pdf-signer-1:/app/tsa_proxy.c 2>/dev/null || true
        docker exec nordiska-v2-pdf-signer-1 gcc -O3 -pthread /app/tsa_proxy.c -o /app/tsa_proxy 2>/dev/null || true
        docker exec -d nordiska-v2-pdf-signer-1 /app/tsa_proxy "$min_ms" "$max_ms" 8082 8081
    else
        docker exec nordiska-v2-pdf-signer-1 pkill tsa_proxy 2>/dev/null || true
    fi
}

recreate_worker() {
    echo -e "\n${C_BOLD_CYAN}Recreating containers with updated environment...${C_RESET}"
    
    local signing
    signing=$(get_env_val "NORDISKA_PDF_ENABLE_SIGNING" "0")
    if [[ "$signing" == "1" ]]; then
        docker compose --env-file "$ENV_FILE" -f "$COMPOSE_MAIN" -f "$COMPOSE_OVERRIDE" up -d --no-deps pdf-signer
    fi
    ensure_tsa_proxy

    docker compose --env-file "$ENV_FILE" -f "$COMPOSE_MAIN" -f "$COMPOSE_OVERRIDE" up -d --no-deps reporting-worker
    STATUS_MSG="[OK] Recreated containers with updated environment!"
    STATUS_COLOR="$C_BOLD_GREEN"
}

read_container_env() {
    local cid="$1"
    local key="$2"
    docker inspect "$cid" --format '{{range .Config.Env}}{{println .}}{{end}}' 2>/dev/null \
        | grep -E "^${key}=" | head -n1 | cut -d'=' -f2- || true
}

render() {
    if [[ -t 1 && "${TERM:-}" != "dumb" ]]; then
        clear 2>/dev/null || true
    fi
    local border="===================================================================="
    local divider="--------------------------------------------------------------------"

    echo -e "${C_CYAN}${border}${C_RESET}"
    echo -e "${C_BOLD_CYAN}         NORDISKA BENCHMARK ENVIRONMENT CONTROLLER (TUI)${C_RESET}"
    echo -e "${C_CYAN}${border}${C_RESET}"

    # Read .env settings
    local cfg_signing cfg_mode cfg_conc cfg_db
    cfg_signing=$(get_env_val "NORDISKA_PDF_ENABLE_SIGNING" "0")
    cfg_mode=$(get_env_val "PDF_SIGNER_MODE" "plain")
    cfg_conc=$(get_env_val "REPORTING_WORKER_CONCURRENCY" "12")
    cfg_db=$(get_env_val "NORDISKA_DATABASE" "nordiska_v2")

    local cfg_tsa_min cfg_tsa_max cfg_tsa_base cfg_tsa_pct
    cfg_tsa_min=$(get_env_val "PDF_SIGNER_TSA_LATENCY_MIN_MS" "")
    cfg_tsa_max=$(get_env_val "PDF_SIGNER_TSA_LATENCY_MAX_MS" "")
    cfg_tsa_base=$(get_env_val "PDF_SIGNER_TSA_LATENCY_MS" "")
    cfg_tsa_pct=$(get_env_val "PDF_SIGNER_TSA_JITTER_PERCENT" "")

    # Read live container status
    local worker_id worker_status live_signing live_mode live_conc
    worker_id=$(docker ps --filter "name=reporting-worker" --format '{{.ID}}' 2>/dev/null | head -n1 || true)
    
    local signer_id signer_status
    signer_id=$(docker ps --filter "name=pdf-signer" --format '{{.ID}}' 2>/dev/null | head -n1 || true)
    
    echo -e "${C_BOLD}TARGET CONFIG (.env):${C_RESET}"
    echo -e "  Database:             ${C_BOLD_YELLOW}${cfg_db}${C_RESET}"
    
    if [[ "$cfg_signing" == "1" ]]; then
        if [[ "$cfg_mode" == "timestamp" || "$cfg_mode" == "1" ]]; then
            echo -e "  Worker Signing:       ${C_BOLD_GREEN}TIMESTAMPED (RFC 3161 TSA Token)${C_RESET}"
        else
            echo -e "  Worker Signing:       ${C_BOLD_CYAN}PLAIN CMS Signature (No TSA)${C_RESET}"
        fi
    else
        echo -e "  Worker Signing:       ${C_YELLOW}DISABLED (Unsigned deterministic PDF)${C_RESET}"
    fi
    echo -e "  Worker Concurrency:   ${C_BOLD}${cfg_conc} slots${C_RESET}"

    if [[ -n "$cfg_tsa_min" && -n "$cfg_tsa_max" ]]; then
        echo -e "  TSA Sim Latency:      ${C_BOLD_GREEN}${cfg_tsa_min}–${cfg_tsa_max} ms (Random Jitter Range)${C_RESET}"
    elif [[ -n "$cfg_tsa_base" && "$cfg_tsa_base" -gt 0 ]]; then
        local pct_label="${cfg_tsa_pct:-50}"
        echo -e "  TSA Sim Latency:      ${C_BOLD_GREEN}${cfg_tsa_base} ms ± ${pct_label}% (Random Jitter Percentage)${C_RESET}"
    else
        echo -e "  TSA Sim Latency:      ${C_GRAY}0 ms (Native in-memory loopback)${C_RESET}"
    fi
    echo -e "${C_GRAY}${divider}${C_RESET}"

    echo -e "${C_BOLD}LIVE WORKER CONTAINER STATUS:${C_RESET}"
    if [[ -n "$worker_id" ]]; then
        worker_status=$(docker ps --filter "id=$worker_id" --format '{{.Names}} ({{.Status}})')
        live_signing=$(read_container_env "$worker_id" "NORDISKA_PDF_ENABLE_SIGNING")
        live_mode=$(read_container_env "$worker_id" "PDF_SIGNER_MODE")
        live_conc=$(read_container_env "$worker_id" "TaxReportWorker__Concurrency")
        [[ -z "$live_mode" ]] && live_mode="plain"
        [[ -z "$live_conc" ]] && live_conc="12"

        echo -e "  Container:            ${C_GREEN}${worker_status}${C_RESET}"
        if [[ "$live_signing" == "1" ]]; then
            if [[ "$live_mode" == "timestamp" || "$live_mode" == "1" ]]; then
                echo -e "  Live Signing:         ${C_BOLD_GREEN}TIMESTAMPED (RFC 3161)${C_RESET}"
            else
                echo -e "  Live Signing:         ${C_BOLD_CYAN}PLAIN CMS SIGNATURE${C_RESET}"
            fi
        else
            echo -e "  Live Signing:         ${C_YELLOW}DISABLED (Unsigned)${C_RESET}"
        fi
        echo -e "  Live Concurrency:     ${C_BOLD}${live_conc} threads${C_RESET}"
    else
        echo -e "  Container:            ${C_BOLD_RED}NOT RUNNING${C_RESET}"
    fi

    if [[ -n "$signer_id" ]]; then
        signer_status=$(docker ps --filter "id=$signer_id" --format '{{.Names}} ({{.Status}})')
        echo -e "  PDF Signer Service:   ${C_GREEN}${signer_status}${C_RESET}"
    else
        echo -e "  PDF Signer Service:   ${C_RED}Stopped${C_RESET}"
    fi

    echo -e "${C_CYAN}${border}${C_RESET}"

    if [[ -n "$STATUS_MSG" ]]; then
        echo -e " ${STATUS_COLOR}${STATUS_MSG}${C_RESET}"
        echo -e "${C_GRAY}${divider}${C_RESET}"
        STATUS_MSG=""
    fi

    echo -e "${C_BOLD_CYAN}Quick Presets (Changes .env & updates worker):${C_RESET}"
    echo -e "  ${C_BOLD}[1]${C_RESET} Set Worker to ${C_YELLOW}UNSIGNED / DISABLED${C_RESET}  (NORDISKA_PDF_ENABLE_SIGNING=0)"
    echo -e "  ${C_BOLD}[2]${C_RESET} Set Worker to ${C_BOLD_CYAN}PLAIN SIGNING${C_RESET}        (NORDISKA_PDF_ENABLE_SIGNING=1, mode=plain)"
    echo -e "  ${C_BOLD}[3]${C_RESET} Set Worker to ${C_BOLD_GREEN}TIMESTAMP SIGNING${C_RESET}    (NORDISKA_PDF_ENABLE_SIGNING=1, mode=timestamp)"
    echo -e ""
    echo -e "${C_BOLD_CYAN}Tuning & Database:${C_RESET}"
    echo -e "  ${C_BOLD}[4]${C_RESET} Change Concurrency Slots    (Current: ${cfg_conc})"
    echo -e "  ${C_BOLD}[5]${C_RESET} Toggle Database             (nordiska_v2 <-> nordiska_synthetic)"
    echo -e "  ${C_BOLD}[6]${C_RESET} Set TSA Latency & Jitter    (2–20ms range or % of base latency)"
    echo -e ""
    echo -e "${C_BOLD_CYAN}Container & Benchmarks:${C_RESET}"
    echo -e "  ${C_BOLD}[A]${C_RESET} ${C_BOLD}Apply & Recreate Worker Now${C_RESET} (~0.5s restart without touching DB)"
    echo -e "  ${C_BOLD}[B]${C_RESET} Run Whole-DB Benchmark      (tools/benchmark/nordiska-benchmark)"
    echo -e "  ${C_BOLD}[D]${C_RESET} Run .NET Dev Benchmark      (worker-pdf-benchmark)"
    echo -e "  ${C_BOLD}[Q]${C_RESET} Quit"
    echo -e "${C_CYAN}${border}${C_RESET}"
}

prompt_apply() {
    echo -e ""
    local ans="y"
    read -r -p "Recreate worker container with new settings now? [Y/n]: " ans || ans="y"
    ans="${ans:-y}"
    if [[ "$ans" =~ ^[Yy]$ ]]; then
        recreate_worker
    else
        STATUS_MSG="[OK] Saved to infra/.env (Container not recreated)."
        STATUS_COLOR="$C_YELLOW"
    fi
}

action_tsa_latency() {
    echo -e "\n${C_BOLD_CYAN}Configure TSA Network Latency & Jitter Simulation:${C_RESET}"
    echo -e "  ${C_BOLD}[1]${C_RESET} 2–20 ms random jitter range (Desynchronized local network)"
    echo -e "  ${C_BOLD}[2]${C_RESET} 10 ms ± 30% jitter percentage (Fast intra-cloud / VNet PKI)"
    echo -e "  ${C_BOLD}[3]${C_RESET} 35 ms ± 25% jitter percentage (Typical commercial TSA)"
    echo -e "  ${C_BOLD}[4]${C_RESET} 80 ms ± 25% jitter percentage (Cross-region WAN / external)"
    echo -e "  ${C_BOLD}[5]${C_RESET} Custom min–max ms range (e.g. 2 20)"
    echo -e "  ${C_BOLD}[6]${C_RESET} Custom base ms + jitter % (e.g. 25ms ± 40%)"
    echo -e "  ${C_BOLD}[0]${C_RESET} Disable simulated latency (0ms native loopback)"
    echo -e ""
    local choice=""
    read -r -p "Select preset [0-6]: " choice || true
    case "$choice" in
        1)
            set_env_val "PDF_SIGNER_TSA_URL" "http://127.0.0.1:8082/"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MIN_MS" "2"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MAX_MS" "20"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MS" ""
            set_env_val "PDF_SIGNER_TSA_JITTER_MS" ""
            set_env_val "PDF_SIGNER_TSA_JITTER_PERCENT" ""
            STATUS_MSG="TSA simulated latency set to 2–20 ms random jitter range (via proxy)."
            STATUS_COLOR="$C_BOLD_GREEN"
            prompt_apply
            ;;
        2)
            set_env_val "PDF_SIGNER_TSA_URL" "http://127.0.0.1:8082/"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MIN_MS" "7"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MAX_MS" "13"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MS" "10"
            set_env_val "PDF_SIGNER_TSA_JITTER_PERCENT" "30"
            STATUS_MSG="TSA latency set to 10 ms ± 30% jitter (via proxy)."
            STATUS_COLOR="$C_BOLD_CYAN"
            prompt_apply
            ;;
        3)
            set_env_val "PDF_SIGNER_TSA_URL" "http://127.0.0.1:8082/"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MIN_MS" "26"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MAX_MS" "44"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MS" "35"
            set_env_val "PDF_SIGNER_TSA_JITTER_PERCENT" "25"
            STATUS_MSG="TSA latency set to 35 ms ± 25% jitter (via proxy)."
            STATUS_COLOR="$C_BOLD_CYAN"
            prompt_apply
            ;;
        4)
            set_env_val "PDF_SIGNER_TSA_URL" "http://127.0.0.1:8082/"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MIN_MS" "60"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MAX_MS" "100"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MS" "80"
            set_env_val "PDF_SIGNER_TSA_JITTER_PERCENT" "25"
            STATUS_MSG="TSA latency set to 80 ms ± 25% jitter (via proxy)."
            STATUS_COLOR="$C_BOLD_YELLOW"
            prompt_apply
            ;;
        5)
            echo -e ""
            local r_min="" r_max=""
            read -r -p "Enter minimum latency in ms: " r_min || true
            read -r -p "Enter maximum latency in ms: " r_max || true
            if [[ "$r_min" =~ ^[0-9]+$ && "$r_max" =~ ^[0-9]+$ ]]; then
                set_env_val "PDF_SIGNER_TSA_URL" "http://127.0.0.1:8082/"
                set_env_val "PDF_SIGNER_TSA_LATENCY_MIN_MS" "$r_min"
                set_env_val "PDF_SIGNER_TSA_LATENCY_MAX_MS" "$r_max"
                set_env_val "PDF_SIGNER_TSA_LATENCY_MS" ""
                set_env_val "PDF_SIGNER_TSA_JITTER_PERCENT" ""
                STATUS_MSG="TSA latency range set to ${r_min}–${r_max} ms (via proxy)."
                STATUS_COLOR="$C_BOLD_GREEN"
                prompt_apply
            fi
            ;;
        6)
            echo -e ""
            local b_ms="" p_pct=""
            read -r -p "Enter base latency in ms (e.g. 20): " b_ms || true
            read -r -p "Enter jitter percentage (e.g. 50 for ±50%): " p_pct || true
            if [[ "$b_ms" =~ ^[0-9]+$ && "$p_pct" =~ ^[0-9]+$ ]]; then
                local j_calc=$(( (b_ms * p_pct) / 100 ))
                local m_low=$(( b_ms - j_calc ))
                [[ $m_low -lt 0 ]] && m_low=0
                local m_high=$(( b_ms + j_calc ))
                set_env_val "PDF_SIGNER_TSA_URL" "http://127.0.0.1:8082/"
                set_env_val "PDF_SIGNER_TSA_LATENCY_MIN_MS" "$m_low"
                set_env_val "PDF_SIGNER_TSA_LATENCY_MAX_MS" "$m_high"
                set_env_val "PDF_SIGNER_TSA_LATENCY_MS" "$b_ms"
                set_env_val "PDF_SIGNER_TSA_JITTER_PERCENT" "$p_pct"
                STATUS_MSG="TSA latency set to ${b_ms} ms ± ${p_pct}% (${m_low}–${m_high} ms via proxy)."
                STATUS_COLOR="$C_BOLD_GREEN"
                prompt_apply
            fi
            ;;
        0)
            set_env_val "PDF_SIGNER_TSA_URL" "http://127.0.0.1:8081/"
            set_env_val "PDF_SIGNER_TSA_LATENCY_MIN_MS" ""
            set_env_val "PDF_SIGNER_TSA_LATENCY_MAX_MS" ""
            set_env_val "PDF_SIGNER_TSA_LATENCY_MS" ""
            set_env_val "PDF_SIGNER_TSA_JITTER_MS" ""
            set_env_val "PDF_SIGNER_TSA_JITTER_PERCENT" ""
            STATUS_MSG="TSA simulated latency disabled (0ms native loopback direct to 8081)."
            STATUS_COLOR="$C_YELLOW"
            prompt_apply
            ;;
    esac
}

main_loop() {
    while true; do
        render
        if ! read -r -p "Select option [1-6, A, B, D, Q]: " opt; then
            opt="q"
        fi
        case "${opt:-}" in
            1)
                set_env_val "NORDISKA_PDF_ENABLE_SIGNING" "0"
                set_env_val "PDF_SIGNER_MODE" "plain"
                set_env_val "PDF_SIGNER_TSA_URL" "http://127.0.0.1:8081/"
                STATUS_MSG="Configured: Unsigned PDF generation (TSA proxy stopped)."
                STATUS_COLOR="$C_BOLD_YELLOW"
                prompt_apply
                ;;
            2)
                set_env_val "NORDISKA_PDF_ENABLE_SIGNING" "1"
                set_env_val "PDF_SIGNER_MODE" "plain"
                set_env_val "PDF_SIGNER_TSA_URL" "http://127.0.0.1:8081/"
                STATUS_MSG="Configured: Plain CMS Signing (TSA proxy stopped)."
                STATUS_COLOR="$C_BOLD_CYAN"
                prompt_apply
                ;;
            3)
                set_env_val "NORDISKA_PDF_ENABLE_SIGNING" "1"
                set_env_val "PDF_SIGNER_MODE" "timestamp"
                local cur_tsa_url
                cur_tsa_url=$(get_env_val "PDF_SIGNER_TSA_URL" "")
                if [[ "$cur_tsa_url" != *"8082"* ]]; then
                    set_env_val "PDF_SIGNER_TSA_URL" "http://127.0.0.1:8082/"
                    set_env_val "PDF_SIGNER_TSA_LATENCY_MIN_MS" "2"
                    set_env_val "PDF_SIGNER_TSA_LATENCY_MAX_MS" "20"
                fi
                STATUS_MSG="Configured: Timestamped Signing (RFC 3161 with 2–20ms Jitter Proxy auto-started)."
                STATUS_COLOR="$C_BOLD_GREEN"
                prompt_apply
                ;;
            4)
                echo -e ""
                local slots=""
                read -r -p "Enter concurrency slots (e.g. 1, 4, 8, 12, 16, 24, 32): " slots || true
                if [[ "$slots" =~ ^[0-9]+$ ]] && [[ "$slots" -gt 0 ]]; then
                    set_env_val "REPORTING_WORKER_CONCURRENCY" "$slots"
                    STATUS_MSG="Worker concurrency set to $slots."
                    STATUS_COLOR="$C_BOLD_CYAN"
                    prompt_apply
                else
                    STATUS_MSG="Invalid concurrency value '$slots'."
                    STATUS_COLOR="$C_BOLD_RED"
                fi
                ;;
            5)
                local cur_db
                cur_db=$(get_env_val "NORDISKA_DATABASE" "nordiska_v2")
                local new_db="nordiska_synthetic"
                if [[ "$cur_db" == "nordiska_synthetic" ]]; then
                    new_db="nordiska_v2"
                fi
                if [[ -x "$NORDISKA_ENV_BIN" ]]; then
                    "$NORDISKA_ENV_BIN" --db "$new_db" >/dev/null 2>&1
                else
                    set_env_val "NORDISKA_DATABASE" "$new_db"
                fi
                STATUS_MSG="Target database switched to $new_db."
                STATUS_COLOR="$C_BOLD_YELLOW"
                prompt_apply
                ;;
            6)
                action_tsa_latency
                ;;
            a|A)
                recreate_worker
                ;;
            b|B)
                ensure_tsa_proxy
                echo -e "\n${C_BOLD_CYAN}Running tools/benchmark/nordiska-benchmark...${C_RESET}\n"
                read -r -p "Optional limit (press Enter for all, or enter number e.g. 100): " limit
                local cmd=("$REPO_ROOT/tools/benchmark/nordiska-benchmark")
                if [[ -n "$limit" && "$limit" =~ ^[0-9]+$ ]]; then
                    cmd+=("--limit" "$limit")
                fi
                "${cmd[@]}" || true
                echo -e "\n${C_GRAY}Press Enter to return to menu...${C_RESET}"
                read -r _
                ;;
            d|D)
                local cfg_signing cfg_mode
                cfg_signing=$(get_env_val "NORDISKA_PDF_ENABLE_SIGNING" "0")
                cfg_mode=$(get_env_val "PDF_SIGNER_MODE" "plain")
                local bench_target="worker-pdf-benchmark"
                if [[ "$cfg_signing" == "1" && "$cfg_mode" == "timestamp" ]]; then
                    bench_target="worker-timestamp-pdf-benchmark"
                fi
                echo -e "\n${C_BOLD_CYAN}Running .NET DevSetup ${bench_target}...${C_RESET}\n"
                read -r -p "Job count [default 1000]: " jobs
                jobs="${jobs:-1000}"
                read -r -p "Timeout in seconds [default 60]: " tout
                tout="${tout:-60}"
                dotnet run --project "$REPO_ROOT/backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj" -- "$REPO_ROOT" "$bench_target" "$jobs" "$tout" || true
                echo -e "\n${C_GRAY}Press Enter to return to menu...${C_RESET}"
                read -r _
                ;;
            q|Q|exit)
                if [[ -t 1 && "${TERM:-}" != "dumb" ]]; then
                    clear 2>/dev/null || true
                fi
                echo -e "${C_BOLD_CYAN}Exiting Benchmark Environment Controller.${C_RESET}\n"
                break
                ;;
            *)
                STATUS_MSG="Invalid option: '${opt}'"
                STATUS_COLOR="$C_BOLD_RED"
                ;;
        esac
    done
}

main_loop
