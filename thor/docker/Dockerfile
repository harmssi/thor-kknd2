# Build image for KKND2 Thor: Android SDK/NDK (from thor-build) + .NET 10 SDK with the android workload.
FROM thor-build
ENV DOTNET_ROOT=/opt/dotnet PATH=/opt/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/di.sh && bash /tmp/di.sh --channel 10.0 --install-dir /opt/dotnet && rm /tmp/di.sh \
 && dotnet workload install android \
 && (yes | /opt/android-sdk/cmdline-tools/latest/bin/sdkmanager "platforms;android-36" "build-tools;36.0.0" >/dev/null || true)
