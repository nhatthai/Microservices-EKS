FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["NET6.WebAPI/NET6.WebAPI.csproj", "NET6.WebAPI/"]
RUN dotnet restore "NET6.WebAPI/NET6.WebAPI.csproj"
COPY . .
WORKDIR "/src/NET6.WebAPI"
RUN dotnet build "NET6.WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NET6.WebAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NET6.WebAPI.dll"]