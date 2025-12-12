FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/Vita3KBot/APIClients/PSNClient/Covers.json ./APIClients/PSNClient/Covers.json
COPY --from=build /src/Vita3KBot/explanations ./explanations

ENTRYPOINT ["dotnet", "Vita3KBot.dll"]
