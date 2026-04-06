# Alpine bases are used on purpose: the Koyeb builder cannot apply opaque
# whiteouts (".wh..wh..opq"), and the Debian-based dotnet images carry them in
# their ca-certificates/openssl layer, which fails every build at the first
# instruction. These digests were verified to contain no opaque whiteouts.
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.20@sha256:d04ba63aae736552b2b03bf0e63efa46d0c765726c831b401044543319d63219 AS build
WORKDIR /src

COPY . .

RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine@sha256:031990dddf16d1a2ce898aad5f0620e750e354f957a172cc462b426e6b019aab
# The alpine runtime image bakes in DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true, which
# makes Discord.Net throw CultureNotFoundException on every GUILD_AVAILABLE dispatch.
RUN apk add --no-cache icu-libs icu-data-full
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/Vita3KBot/APIClients/PSNClient/Covers.json ./APIClients/PSNClient/Covers.json
COPY --from=build /src/Vita3KBot/explanations ./explanations

ENTRYPOINT ["dotnet", "Vita3KBot.dll"]
