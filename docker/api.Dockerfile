FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PHCIS.ClinicBookingTyrone.slnx ./
COPY ClinicBooking.Domain/ClinicBooking.Domain.csproj ClinicBooking.Domain/
COPY ClinicBooking.Application/ClinicBooking.Application.csproj ClinicBooking.Application/
COPY ClinicBooking.Infrastructure/ClinicBooking.Infrastructure.csproj ClinicBooking.Infrastructure/
COPY ClinicBooking.Api/ClinicBooking.Api.csproj ClinicBooking.Api/

RUN dotnet restore ClinicBooking.Api/ClinicBooking.Api.csproj

COPY ClinicBooking.Domain/ ClinicBooking.Domain/
COPY ClinicBooking.Application/ ClinicBooking.Application/
COPY ClinicBooking.Infrastructure/ ClinicBooking.Infrastructure/
COPY ClinicBooking.Api/ ClinicBooking.Api/

RUN dotnet publish ClinicBooking.Api/ClinicBooking.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClinicBooking.Api.dll"]
