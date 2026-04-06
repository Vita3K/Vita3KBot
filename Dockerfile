# Alpine bases are used on purpose: the Koyeb builder cannot apply opaque
# whiteouts (".wh..wh..opq"), and the Debian-based dotnet images carry them in
# their ca-certificates/openssl layer, which fails every build at the first
# instruction. These digests were verified to contain no opaque whiteouts.
#
# The SDK is pinned to 10.0.200 rather than a floating 10.0 tag: every alpine SDK
# image from 10.0.201 onwards ships a "tmp/.dotnet/.wh..wh..opq" layer, which the
# Koyeb builder cannot apply. 10.0.200 still builds net10.0 fine.
FROM mcr.microsoft.com/dotnet/sdk:10.0.200-alpine3.23@sha256:a0116e63beedf9197c3d491eb224aea9ae7d1692079eda9eebe2809f06d580e3 AS build
WORKDIR /src

COPY . .

RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0.11-alpine3.24@sha256:216f4e2027da6ae806e0bc4b448669ac0faa00125908e308f31dd70598e58136
# The alpine runtime image bakes in DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true, which
# makes Discord.Net throw CultureNotFoundException on every GUILD_AVAILABLE dispatch.
RUN apk add --no-cache icu-libs icu-data-full
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/Vita3KBot/APIClients/PSNClient/Covers.json ./APIClients/PSNClient/Covers.json
COPY --from=build /src/Vita3KBot/explanations ./explanations

ENTRYPOINT ["dotnet", "Vita3KBot.dll"]
