FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["NemesisBakuApi.csproj", "./"]
RUN dotnet restore "NemesisBakuApi.csproj"

COPY . .
RUN dotnet publish "NemesisBakuApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "NemesisBakuApi.dll"]