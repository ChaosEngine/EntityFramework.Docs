FROM mcr.microsoft.com/dotnet/core/sdk:2.2-stretch AS build
RUN curl -sL https://deb.nodesource.com/setup_11.x | bash - && \
	apt-get install -y nodejs
RUN npm i gulp@latest -g
WORKDIR /build

ENV DBKind="sqlite" ConnectionStrings__Sqlite="Filename=./bin/Debug/netcoreapp2.2/Blogging.db"

COPY ./InkBall/src/InkBall.Module/InkBall.Module.csproj ./InkBall/src/InkBall.Module/InkBall.Module.csproj
COPY ./InkBall/test/InkBall.Tests/InkBall.Tests.csproj ./InkBall/test/InkBall.Tests/InkBall.Tests.csproj
COPY ./AspNetCore.ExistingDb.Tests/AspNetCore.ExistingDb.Tests.csproj ./AspNetCore.ExistingDb.Tests/AspNetCore.ExistingDb.Tests.csproj
COPY ./Caching-MySQL/src/Pomelo.Extensions.Caching.MySqlConfig.Tools/Pomelo.Extensions.Caching.MySqlConfig.Tools.csproj ./Caching-MySQL/src/Pomelo.Extensions.Caching.MySqlConfig.Tools/Pomelo.Extensions.Caching.MySqlConfig.Tools.csproj
COPY ./Caching-MySQL/src/Pomelo.Extensions.Caching.MySql/Pomelo.Extensions.Caching.MySql.csproj ./Caching-MySQL/src/Pomelo.Extensions.Caching.MySql/Pomelo.Extensions.Caching.MySql.csproj
COPY ./Caching-MySQL/test/Pomelo.Extensions.Caching.MySql.Tests/Pomelo.Extensions.Caching.MySql.Tests.csproj ./Caching-MySQL/test/Pomelo.Extensions.Caching.MySql.Tests/Pomelo.Extensions.Caching.MySql.Tests.csproj
COPY ./IdentityManager2/src/IdentityManager2/IdentityManager2.csproj ./IdentityManager2/src/IdentityManager2/IdentityManager2.csproj
COPY ./IdentityManager2/src/IdentityManager2/Assets/ ./IdentityManager2/src/IdentityManager2/Assets/
COPY ./DevReload/DevReload.csproj ./DevReload/DevReload.csproj
COPY ./AspNetCore.ExistingDb/AspNetCore.ExistingDb.csproj ./AspNetCore.ExistingDb/AspNetCore.ExistingDb.csproj
COPY ./*.sln ./NuGet.config ./
RUN dotnet restore -r linux-x64

COPY . .
RUN dotnet test -v m
RUN dotnet publish -c Release -r linux-x64 \
    #-p:PublishWithAspNetCoreTargetManifest=false #remove this after prerelease patch publish \
	/p:ShowLinkerSizeComparison=true /p:CrossGenDuringPublish=false \
    AspNetCore.ExistingDb





FROM mcr.microsoft.com/dotnet/core/runtime-deps:2.2-stretch-slim
WORKDIR /app
COPY --from=build --chown=www-data:www-data /build/AspNetCore.ExistingDb/bin/Release/netcoreapp2.2/linux-x64/publish/ /build/startApp.sh ./

ENV TZ=Europe/Warsaw USER=www-data ASPNETCORE_URLS=http://+:5000
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

USER "$USER"

VOLUME /shared
EXPOSE 5000

ENTRYPOINT ["./AspNetCore.ExistingDb"]
