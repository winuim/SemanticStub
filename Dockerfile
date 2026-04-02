FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["src/SemanticStub.Api/SemanticStub.Api.csproj", "src/SemanticStub.Api/"]
RUN dotnet restore "./src/SemanticStub.Api/SemanticStub.Api.csproj"
COPY . .
WORKDIR "/src/src/SemanticStub.Api"
RUN dotnet build "./SemanticStub.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SemanticStub.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=build /src/samples ./samples
ENTRYPOINT ["dotnet", "SemanticStub.Api.dll"]
