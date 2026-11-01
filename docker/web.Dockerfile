FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG API_BASE_URL=http://localhost:8080/

COPY ClinicBooking.Blazor/ClinicBooking.Blazor.csproj ClinicBooking.Blazor/
RUN dotnet restore ClinicBooking.Blazor/ClinicBooking.Blazor.csproj

COPY ClinicBooking.Blazor/ ClinicBooking.Blazor/
RUN printf '%s\n' "{\"ApiBaseUrl\":\"${API_BASE_URL}\"}" > ClinicBooking.Blazor/wwwroot/appsettings.json
RUN dotnet publish ClinicBooking.Blazor/ClinicBooking.Blazor.csproj -c Release -o /app

FROM nginx:1.27-alpine AS runtime
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/wwwroot /usr/share/nginx/html
EXPOSE 80
