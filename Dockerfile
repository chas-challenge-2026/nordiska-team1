# Multi-stage Dockerfile for Nordiska Sparbanken v2
# Builds Native C++ modules, React frontend SPA, and .NET 8 Web API into a single production container

# Stage 1: Build Native C++ PDF Generator & C API
FROM gcc:13-bookworm AS native-builder
WORKDIR /src/native/pdf_generator

RUN apt-get update && apt-get install -y --no-install-recommends \
    cmake \
    pkg-config \
    libcairo2-dev \
    libhpdf-dev \
    nlohmann-json3-dev \
    && rm -rf /var/lib/apt/lists/*

# Provide CMake config bridge for Debian's system libhpdf
RUN mkdir -p /usr/local/lib/cmake/unofficial-libharu && \
    printf 'add_library(unofficial::libharu::hpdf UNKNOWN IMPORTED)\nfind_library(HPDF_LIB NAMES hpdf libhpdf REQUIRED)\nset_target_properties(unofficial::libharu::hpdf PROPERTIES IMPORTED_LOCATION "${HPDF_LIB}" INTERFACE_INCLUDE_DIRECTORIES "/usr/include")\n' > /usr/local/lib/cmake/unofficial-libharu/unofficial-libharu-config.cmake

COPY native/pdf_generator/ ./

RUN cmake -B build \
    -DCMAKE_BUILD_TYPE=Release \
    -DBUILD_TESTING=OFF \
    && cmake --build build --config Release --target nordiska_document_c_api

# Stage 2: Build React frontend
FROM node:20-alpine AS frontend-builder
WORKDIR /app/frontend

COPY frontend/package*.json ./
RUN npm install

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

# Install runtime libraries for Cairo & Haru PDF rendering
RUN apt-get update && apt-get install -y --no-install-recommends \
    libcairo2 \
    libhpdf-2.3.0 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=backend-builder /app/publish .
COPY --from=frontend-builder /app/frontend/dist ./wwwroot
COPY --from=native-builder /src/native/pdf_generator/build/libnordiska_document_c_api.so /app/
COPY --from=native-builder /src/native/pdf_generator/build/libnordiska_document_c_api.so /usr/local/lib/
RUN ldconfig

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV LD_LIBRARY_PATH=/app:/usr/local/lib

ENTRYPOINT ["dotnet", "Nordiska.FrontendApi.dll"]
