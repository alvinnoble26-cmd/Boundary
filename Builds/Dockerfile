FROM ubuntu:22.04

ARG DEBIAN_FRONTEND=noninteractive
ARG SERVER_BUILD_PATH=Builds/EdgegapServer

RUN apt-get update && \
    apt-get install -y ca-certificates libsecret-1-0 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    update-ca-certificates

COPY ${SERVER_BUILD_PATH} /root/build/

WORKDIR /root/

ENV LD_LIBRARY_PATH=/root/build/ServerBuild_Data/Plugins

RUN chmod +x /root/build/ServerBuild.x86_64

CMD ["/bin/bash", "-c", "/root/build/ServerBuild.x86_64 -batchmode -nographics $UNITY_COMMANDLINE_ARGS"]
