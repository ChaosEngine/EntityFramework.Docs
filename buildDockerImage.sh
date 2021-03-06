#!/bin/bash
#
	docker run -it --rm -v $(pwd):/app --workdir /app \
		--env DBKind=sqlite \
		--env ConnectionStrings__Sqlite="Filename=./bin/Debug/netcoreapp2.0/Blogging.db"  \
		microsoft/aspnetcore-build:latest bash -c \
		"cd Caching-MySQL && dotnet restore && cd .. && \
		dotnet test -v n AspNetCore.ExistingDb.Tests && \
		dotnet publish -c Release \
			-p:PublishWithAspNetCoreTargetManifest=false #remove this afer prerelease patch publish \
			AspNetCore.ExistingDb && \
		find AspNetCore.ExistingDb*/bin/Release/netcoreapp2.0/publish/ -type d -exec chmod ug=rwx,o=rx {} \; && \
		find AspNetCore.ExistingDb*/bin/Release/netcoreapp2.0/publish/ AspNetCore.ExistingDb/wwwroot/{js,css}/ -type f -exec chmod ug=rw,o=r {} \;" && \
	docker build -t chaosengine/aspnetcore:latest . && \
	chmod 777 shared

#
# Build:
# docker build -t chaosengine/aspnetcore:latest .
# docker build -t chaosengine/aspnetcore:alpine .
#
# Run:
# docker run -it --rm -p 8080:5000 --name aspnetcore --env-file docker-env.txt -v /home/container/EntityFramework.Docs/sockets:/sockets -v /home/container/EntityFramework.Docs/shared:/shared -v /var/www/localhost/htdocs/webcamgallery:/webcamgallery chaosengine/aspnetcore
# docker run -it --rm -p 8080:5000 --name aspnetcore --env-file docker-env.txt -v /home/container/EntityFramework.Docs/sockets:/sockets -v /home/container/EntityFramework.Docs/shared:/shared -v /var/www/localhost/htdocs/webcamgallery:/webcamgallery chaosengine/aspnetcore:alpine
#
