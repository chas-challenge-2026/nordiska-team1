# Multi-stage Dockerfile for Nordiska Sparbanken v2
# Builds the native PDF runtime, React frontend, Web API, and Reporting Worker.

# Stage 1: Build Native C++ PDF Generator & C API
FROM gcc:13-bookworm AS native-builder
WORKDIR /src/native

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    cmake \
    git \
    ninja-build \
    perl \
    pkg-config \
    wget \
    zlib1g-dev \
    libcairo2-dev \
    libhpdf-dev \
    nlohmann-json3-dev \
    && rm -rf /var/lib/apt/lists/*

# Provide CMake config bridge for Debian's system libhpdf
RUN mkdir -p /usr/local/lib/cmake/unofficial-libharu && \
    printf 'add_library(unofficial::libharu::hpdf UNKNOWN IMPORTED)\nfind_library(HPDF_LIB NAMES hpdf libhpdf REQUIRED)\nset_target_properties(unofficial::libharu::hpdf PROPERTIES IMPORTED_LOCATION "${HPDF_LIB}" INTERFACE_INCLUDE_DIRECTORIES "/usr/include")\n' > /usr/local/lib/cmake/unofficial-libharu/unofficial-libharu-config.cmake

COPY native/pdf_generator/ ./pdf_generator/
COPY native/pdf-signer/ ./pdf-signer/

WORKDIR /src/native/pdf_generator

RUN sed 's/\r$//' ./tools/setup-openssl-3.3.sh | bash

RUN cmake -B build \
    -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DBUILD_TESTING=OFF \
    -DOPENSSL_ROOT_DIR=/root/.local/openssl-3.3 \
    -DCMAKE_SHARED_LINKER_FLAGS="-static-libstdc++ -static-libgcc" \
    -DOPENSSL_USE_STATIC_LIBS=TRUE \
    && cmake --build build --config Release --target nordiska_pdf_generator_c_api

# Stage 2: Restore React frontend dependencies
FROM node:20-alpine AS frontend-dependencies
WORKDIR /app/frontend

COPY frontend/package*.json ./
RUN npm ci

# Stage 3: React development server
FROM frontend-dependencies AS frontend-development

COPY frontend/ ./

EXPOSE 5173

ENV VITE_API_BASE_URL=http://localhost:5031/api
ENV CHOKIDAR_USEPOLLING=true

CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0", "--port", "5173"]

# Stage 4: Build the production React frontend
FROM frontend-dependencies AS frontend-builder

COPY frontend/ ./
RUN VITE_API_BASE_URL=/api npm run build

# Stage 5: Build .NET 8 Web API and Reporting Worker
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
COPY backend/src/Modules/Inbox/*.csproj ./backend/src/Modules/Inbox/
COPY backend/src/Nordiska.Reporting.Worker/*.csproj ./backend/src/Nordiska.Reporting.Worker/

RUN dotnet restore ./backend/src/Nordiska.FrontendApi/Nordiska.FrontendApi.csproj
RUN dotnet restore ./backend/src/Nordiska.Reporting.Worker/Nordiska.Reporting.Worker.csproj

COPY backend/src/ ./backend/src/
RUN dotnet publish ./backend/src/Nordiska.FrontendApi/Nordiska.FrontendApi.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

RUN dotnet publish ./backend/src/Nordiska.Reporting.Worker/Nordiska.Reporting.Worker.csproj \
    -c Release \
    -o /app/worker \
    /p:UseAppHost=false

# Stage 6: Shared .NET runtime with the native PDF library
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime-base
WORKDIR /app

# Install runtime libraries for Cairo & Haru PDF rendering
RUN apt-get update && apt-get install -y --no-install-recommends \
    libcairo2 \
    libhpdf-2.3.0 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=native-builder /src/native/pdf_generator/build/libnordiska_pdf_generator_c_api.so /usr/local/lib/
RUN ldconfig

# Stage 7: Reporting Worker runtime
FROM runtime-base AS reporting-worker

COPY --from=backend-builder /app/worker .

RUN mkdir -p /var/lib/nordiska/report-documents \
    && chown -R $APP_UID:$APP_UID /var/lib/nordiska/report-documents

ENV NORDISKA_PDF_ENABLE_SIGNING=0
ENV ReportDocumentStorage__RootPath=/var/lib/nordiska/report-documents

USER $APP_UID

ENTRYPOINT ["dotnet", "Nordiska.Reporting.Worker.dll"]

# Stage 8: Backend-only Web API runtime for local frontend development
FROM runtime-base AS api

COPY --from=backend-builder /app/publish .

RUN mkdir -p /var/lib/nordiska/report-documents \
    && chown -R $APP_UID:$APP_UID /var/lib/nordiska/report-documents

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ReportDocumentStorage__RootPath=/var/lib/nordiska/report-documents

# Run container as unprivileged non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "Nordiska.FrontendApi.dll"]

# Stage 9: Existing combined Web API + React runtime
FROM runtime-base AS final

COPY --from=backend-builder /app/publish .
COPY --from=frontend-builder /app/frontend/dist ./wwwroot

RUN mkdir -p /var/lib/nordiska/report-documents \
    && chown -R $APP_UID:$APP_UID /var/lib/nordiska/report-documents

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ReportDocumentStorage__RootPath=/var/lib/nordiska/report-documents

# Run container as unprivileged non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "Nordiska.FrontendApi.dll"]

# Stage 10: Database Migrator (Devtools) for isolated schema migrations
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS migrator
WORKDIR /src
COPY backend/Directory.Build.props backend/Directory.Packages.props ./backend/
COPY backend/dotnet-tools.json ./backend/
COPY backend/src/ ./backend/src/
COPY backend/scripts/migrate-database.sh ./backend/scripts/
RUN chmod +x ./backend/scripts/migrate-database.sh
WORKDIR /src/backend
RUN dotnet tool restore
ENTRYPOINT ["/src/backend/scripts/migrate-database.sh"]