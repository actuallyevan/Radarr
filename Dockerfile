# syntax=docker/dockerfile:1
ARG BASE_IMAGE=ghcr.io/linuxserver/radarr:latest

FROM scratch AS binaries
ARG TARGETARCH
COPY _artifacts/linux-musl-x64/net8.0/Radarr /amd64
COPY _artifacts/linux-musl-arm64/net8.0/Radarr /arm64

FROM ${BASE_IMAGE}

ARG BUILD_DATE
ARG VERSION
ARG RADARR_BRANCH=master
ARG PACKAGE_AUTHOR="github.com/actuallyevan/Radarr"
ARG TARGETARCH

LABEL org.opencontainers.image.created="${BUILD_DATE}" \
  org.opencontainers.image.source="${PACKAGE_AUTHOR}" \
  org.opencontainers.image.version="${VERSION}"

RUN rm -rf /app/radarr/bin/*

COPY --chmod=755 --from=binaries /${TARGETARCH}/. /app/radarr/bin/

RUN echo -e "UpdateMethod=docker\nBranch=${RADARR_BRANCH}\nPackageVersion=${VERSION:-LocalBuild}\nPackageAuthor=${PACKAGE_AUTHOR}" > /app/radarr/package_info && \
  printf "Linuxserver.io version: ${VERSION}\nBuild-date: ${BUILD_DATE}" > /build_version && \
  echo "**** cleanup ****" && \
  rm -rf \
    /app/radarr/bin/Radarr.Update
