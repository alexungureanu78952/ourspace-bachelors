FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN --mount=type=cache,id=ourspace-nuget-locked,sharing=locked,target=/root/.nuget/packages dotnet restore "$PROJECT" --locked-mode
RUN --mount=type=cache,id=ourspace-nuget-locked,sharing=locked,target=/root/.nuget/packages dotnet publish "$PROJECT" --no-restore -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac
WORKDIR /app
RUN mkdir -p /var/ourspace/keys && chown app:app /var/ourspace/keys
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    SSL_CERT_FILE=/run/secrets/ca.crt
USER app
EXPOSE 8080
