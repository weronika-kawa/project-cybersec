FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /src
COPY backend backend
COPY tests tests
RUN dotnet restore tests/SecureBank.Tests.csproj
CMD ["dotnet","test","tests/SecureBank.Tests.csproj","--logger","console;verbosity=normal"]
