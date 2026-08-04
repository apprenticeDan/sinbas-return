# ============================================================
# DEPRECADO — Este archivo se mantiene solo como referencia.
# La imagen de desarrollo se construye desde:
#   base/Containerfile.dotnet.base
#
# Para construir:
#   cd base && ./build-dotnet-base.sh 1.0.0
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
RUN apt-get update && apt-get install -y git && rm -rf /var/lib/apt/lists/*
WORKDIR /workspace
EXPOSE 8080
CMD ["bash"]