# Multi-stage Dockerfile for Nordiska Sparbanken v2
# Builds Native C++ modules, React frontend SPA, and .NET 8 Web API into a single production container

# Stage 0: Build modern OpenSSL (3.3.2) with CMS_final_digest support
FROM debian:bookworm-slim AS openssl-builder
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential ca-certificates wget perl \
    && rm -rf /var/lib/apt/lists/*
RUN wget -q https://github.com/openssl/openssl/releases/download/openssl-3.3.2/openssl-3.3.2.tar.gz && \
    tar -xzf openssl-3.3.2.tar.gz && \
    cd openssl-3.3.2 && \
    ./config --prefix=/usr/local --openssldir=/usr/local/ssl no-docs no-tests shared && \
    make -j$(nproc) && \
    make install_sw

# Stage 1: Build Native C++ PDF Generator & C API
FROM gcc:13-bookworm AS native-builder
WORKDIR /src

RUN apt-get update && apt-get install -y --no-install-recommends \
    cmake=3.25.1-1 \
    pkg-config=1.8.1-1 \
    libcairo2-dev=1.16.0-7 \
    libhpdf-dev=2.3.0+dfsg-1+b1 \
    nlohmann-json3-dev=3.11.2-2 \
    zlib1g-dev=1:1.2.13.dfsg-1 \
    libsimdjson-dev=3.0.1-1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=openssl-builder /usr/local /usr/local
RUN printf '/usr/local/lib\n/usr/local/lib64\n' > /etc/ld.so.conf.d/usr-local.conf && ldconfig

# Provide CMake config bridge for Debian's system libhpdf
RUN mkdir -p /usr/local/lib/cmake/unofficial-libharu && \
    printf 'add_library(unofficial::libharu::hpdf UNKNOWN IMPORTED)\nfind_library(HPDF_LIB NAMES hpdf libhpdf REQUIRED)\nset_target_properties(unofficial::libharu::hpdf PROPERTIES IMPORTED_LOCATION "${HPDF_LIB}" INTERFACE_INCLUDE_DIRECTORIES "/usr/include")\n' > /usr/local/lib/cmake/unofficial-libharu/unofficial-libharu-config.cmake

COPY native/ native/
WORKDIR /src/native/pdf_generator

RUN cmake -B build \
    -DCMAKE_BUILD_TYPE=Release \
    -DFETCHCONTENT_FULLY_DISCONNECTED=ON \
    -DBUILD_TESTING=OFF \
    && cmake --build build --config Release --target nordiska_pdf_generator_c_api

# Stage 2: Build React frontend
FROM node:20-alpine AS frontend-builder
WORKDIR /app/frontend

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ ./
RUN VITE_API_BASE_URL=/api npm run build

# Stage 3: Build .NET 8 Web API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-builder
WORKDIR /src

# Copy project files for optimal layer caching
COPY backend/Directory.Build.props ./backend/
COPY backend/Directory.Packages.props ./backend/
COPY backend/src/Nordiska.FrontendApi/*.csproj ./backend/src/Nordiska.FrontendApi/
COPY backend/src/BuildingBlocks/Database/*.csproj ./backend/src/BuildingBlocks/Database/
COPY backend/src/Modules/Banking/*.csproj ./backend/src/Modules/Banking/
COPY backend/src/Modules/Faq/*.csproj ./backend/src/Modules/Faq/
COPY backend/src/Modules/Reporting/*.csproj ./backend/src/Modules/Reporting/

RUN dotnet restore ./backend/src/Nordiska.FrontendApi/Nordiska.FrontendApi.csproj

COPY backend/src/ ./backend/src/
RUN dotnet publish ./backend/src/Nordiska.FrontendApi/Nordiska.FrontendApi.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Stage 4: Final ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install runtime libraries for Cairo & Haru PDF rendering + simdjson + openssl
RUN apt-get update && apt-get install -y --no-install-recommends \
    libcairo2=1.16.0-7 \
    libhpdf-2.3.0=2.3.0+dfsg-1+b1 \
    libsimdjson14=3.0.1-1 \
    openssl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=openssl-builder /usr/local/lib64/libcrypto* /usr/local/lib/
COPY --from=openssl-builder /usr/local/lib64/libssl* /usr/local/lib/
COPY --from=openssl-builder /usr/local/lib64/ossl-modules /usr/local/lib64/ossl-modules
RUN printf '/usr/local/lib\n/usr/local/lib64\n' > /etc/ld.so.conf.d/usr-local.conf && ldconfig

# Enable both default and legacy OpenSSL providers for modern signing + legacy BankID certificates
RUN printf 'openssl_conf = openssl_init\n\n[openssl_init]\nproviders = provider_sect\n\n[provider_sect]\ndefault = default_sect\nlegacy = legacy_sect\n\n[default_sect]\nactivate = 1\n\n[legacy_sect]\nactivate = 1\n' > /etc/ssl/openssl.cnf

# Generate Nordiska digital signing certificate & key
RUN mkdir -p /app/certs && \
    openssl req -config /etc/ssl/openssl.cnf -x509 -newkey rsa:2048 -nodes \
        -keyout /app/certs/signing_key.pem \
        -out /app/certs/signing_cert.pem \
        -days 3650 \
        -subj "/C=SE/O=Nordiska Sparbanken/CN=Nordiska PDF Signer" && \
    chown -R $APP_UID:$APP_UID /app/certs && \
    chmod 600 /app/certs/signing_key.pem && \
    chmod 644 /app/certs/signing_cert.pem

COPY --from=backend-builder /app/publish .
COPY --from=frontend-builder /app/frontend/dist ./wwwroot
COPY --from=native-builder /usr/local/lib64/libstdc++.so.6* /usr/local/lib/
COPY --from=native-builder /src/native/pdf_generator/build/libnordiska_pdf_generator_c_api.so /usr/local/lib/
RUN ldconfig

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV OPENSSL_MODULES=/usr/local/lib64/ossl-modules
ENV OPENSSL_CONF=/etc/ssl/openssl.cnf
ENV PDF_SIGNER_KEY_PATH=/app/certs/signing_key.pem
ENV PDF_SIGNER_CERT_PATH=/app/certs/signing_cert.pem
ENV NORDISKA_PDF_ENABLE_SIGNING=true

# Run container as unprivileged non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "Nordiska.FrontendApi.dll"]
