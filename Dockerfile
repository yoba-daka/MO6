#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MO6.csproj", "."]
RUN dotnet restore "./MO6.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./MO6.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MO6.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .


## --- SSH & SFTP SETUP START ---
#RUN apt-get update \
    #&& apt-get install -y --no-install-recommends \
         #openssh-server \
         #unzip \
         #curl \
    #&& mkdir -p /var/run/sshd \
    #&& ssh-keygen -A \
    #&& echo 'root:Docker!' | chpasswd
#
## inline sshd_config with KexAlgorithms for VS/liblinux compatibility
#RUN mkdir -p /etc/ssh \
 #&& cat <<-'EOF' > /etc/ssh/sshd_config
#Port 2222
#ListenAddress 0.0.0.0
#LoginGraceTime 180
#X11Forwarding yes
#KexAlgorithms curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,diffie-hellman-group14-sha1
#Ciphers aes128-cbc,3des-cbc,aes256-cbc,aes128-ctr,aes192-ctr,aes256-ctr
#MACs hmac-sha1,hmac-sha1-96
#StrictModes yes
#SyslogFacility DAEMON
#PasswordAuthentication yes
#PermitEmptyPasswords no
#PermitRootLogin yes
#Subsystem sftp internal-sftp
#EOF
#
#EXPOSE 2222
## --- SSH & SFTP SETUP END ---



# Supply runtime secrets through environment variables or the hosting platform.
# Do not commit connection strings, storage keys, or API credentials here.

#ENTRYPOINT ["/bin/bash","-c","/usr/sbin/sshd -D & exec dotnet MO6.dll"]
ENTRYPOINT ["dotnet", "MO6.dll"]


