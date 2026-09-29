# Build from the repository root: docker build -f FormFlow.Blazor/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY FormFlow.Data/FormFlow.Data.csproj FormFlow.Data/
COPY FormFlow.Blazor/FormFlow.Blazor.csproj FormFlow.Blazor/
RUN dotnet restore FormFlow.Blazor/FormFlow.Blazor.csproj
COPY FormFlow.Data/ FormFlow.Data/
COPY FormFlow.Blazor/ FormFlow.Blazor/
RUN dotnet publish FormFlow.Blazor/FormFlow.Blazor.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
# BackendApi__BaseUrl must point at the API as this container sees it.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DisableHttpsRedirection=true \
    BackendApi__BaseUrl=http://api:8080/
EXPOSE 8080
ENTRYPOINT ["dotnet", "FormFlow.Blazor.dll"]
