# Build from the repository root: docker build -f FormFlow.Backend/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY FormFlow.Data/FormFlow.Data.csproj FormFlow.Data/
COPY FormFlow.Backend/FormFlow.Backend.csproj FormFlow.Backend/
RUN dotnet restore FormFlow.Backend/FormFlow.Backend.csproj
COPY FormFlow.Data/ FormFlow.Data/
COPY FormFlow.Backend/ FormFlow.Backend/
RUN dotnet publish FormFlow.Backend/FormFlow.Backend.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# LiteDB keeps its file in /data so a volume can hold it between restarts.
RUN mkdir /data && chown $APP_UID /data
VOLUME /data
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DisableHttpsRedirection=true \
    ConnectionStrings__LiteDb="Filename=/data/formflow.db;Connection=shared"
EXPOSE 8080
ENTRYPOINT ["dotnet", "FormFlow.Backend.dll"]
