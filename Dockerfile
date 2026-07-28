# syntax=docker/dockerfile:1
ARG BASE_IMAGE=ghcr.io/linuxserver/radarr:latest
FROM ${BASE_IMAGE}

ARG BUILD_DATE
ARG VERSION
ARG RADARR_BRANCH=master
ARG PACKAGE_AUTHOR="github.com/actuallyevan/Radarr"
ARG TARGETARCH

LABEL org.opencontainers.image.created="${BUILD_DATE}" \
  org.opencontainers.image.source="${PACKAGE_AUTHOR}" \
  org.opencontainers.image.version="${VERSION}"

COPY _artifacts/linux-musl-x64/net8.0/Radarr /tmp/radarr-x64
COPY _artifacts/linux-musl-arm64/net8.0/Radarr /tmp/radarr-arm64

RUN mkdir -p /app/radarr/bin && \
  if [ "$TARGETARCH" = "amd64" ]; then \
    cp -r /tmp/radarr-x64/* /app/radarr/bin/; \
  elif [ "$TARGETARCH" = "arm64" ]; then \
    cp -r /tmp/radarr-arm64/* /app/radarr/bin/; \
  else \
    echo "Unsupported TARGETARCH: $TARGETARCH" >&2; exit 1; \
  fi && \
  rm -rf /tmp/radarr-x64 /tmp/radarr-arm64 && \
  echo -e "UpdateMethod=docker\nBranch=${RADARR_BRANCH}\nPackageVersion=${VERSION:-LocalBuild}\nPackageAuthor=${PACKAGE_AUTHOR}" > /app/radarr/package_info && \
  printf "Linuxserver.io version: ${VERSION}\nBuild-date: ${BUILD_DATE}" > /build_version && \
  echo "**** cleanup ****" && \
  rm -rf \
    /app/radarr/bin/Radarr.Update
