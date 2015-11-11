# Hot to fix Nokia E71 gmail connectivity issues (valid for 11.11.2015)

This solution is designed to make an encrypted (SHA1RSA) SMTP, IMAP, POP proxy to fix the connectivity problems for NOKIA E71 device with Google SSL Certificates (the device's OS does not allow to use SHA2RSA certs)...

## The architecture of the solution

 - You should have a valid gmail account
 - Your should generate self signed certificates for the application to encrypt all your traffic and stay secure
 - You should have a linux server with public static IP address and well known DNS name with docker installed (how to install docker on the server - see below)
 - Connect your phone to your server (all traffic is encrypted and routed to gmail)

## Setup host

Does not matter which hosting provider you will use, but this example defines the digitalocean setup.

1. Go to https://www.digitalocean.com/
2. Login with your username or signup (if your signup you will have to attach your credit card)
3. Choose "Create Droplet"
4. For "Droplet Hostname" enter something like "nokiamail", select size (5$/month is the best option), the choose the nearest availibe Datacenter to you, for OS select "Ubuntu 14.04 x64", enter SSH public key if you have (if you do not know what is SSH key, enter nothing - the root password will be sent to you by email)
5. Click "Create", wait for droplet creation and login to it using SSH client (console for mac/linux), Putty for Windows

## Install docker
1. SSH onto your server with password or SSH public key auth
    ```ssh
    $ ssh root@[your server ip address]
    ```
2. Install docker with single command
    ```ssh
    $ curl -L get.docker.com | sh
    ```
3. It is done!
 
## Generate certificates 

1. create a directory, go to it, generate self-signed certs (script is below)
    ```sh
    $ mkdir certs
    $ cd certs
    # generate self-signed Certificate Authority
    # enter some data for generation (password for private keys, data for CA authority)
    $ openssl genrsa -des3 -out ca.key 2048
    $ openssl req -x509 -new -nodes -key ca.key -days 1024 -out ca.pem
    
    # generate application certificate
    # for [CN] (Common name) enter the IP address (or dns name if exists) of your server!
    $ openssl genrsa -out mail.key 2048
    $ openssl req -new -key mail.key -out mail.csr
    $ openssl x509 -req -in mail.csr -CA ca.pem -CAkey ca.key -CAcreateserial -out mail.crt -sha1 -days 500
    
    # export the application certificate into PFX format for application usage
    $ openssl pkcs12 -export -out mail.pfx -inkey mail.key -in mail.crt -certfile ca.pem
    ```
2. Copy the mail.pfx file to your server into /root directory ``` $ cp mail.pfx /root ```
 
## Build the application

Download the sources to your server and build the application
```sh
$ git clone https://github.com/rbarinov/imap-gmail-nokia-e71-fix.git e71
$ cd e71
# build 
$ docker build -t e71 ./
# now the application is ready to run on your server
```
    
## Install the application

If you want to log all traffic, just add "-e LOG_LEVEL=TRACE " after each "docker run" and before "-v /root/...."

```sh
# start imap proxy
docker run -v /root/mail.pfx:/usr/src/app/build/mail.pfx:ro -d --restart=always --name=e71-imap -e LOCAL_PORT=993 -e HOST=imap.gmail.com -e PORT=993 -p 993:993 e71 bash -c 'mono testimap.exe $LOCAL_PORT $HOST $PORT ./mail.pfx'

# start smtp proxy
docker run -v /root/mail.pfx:/usr/src/app/build/mail.pfx:ro -d --restart=always --name=e71-smtp -e LOCAL_PORT=465 -e HOST=smtp.gmail.com -e PORT=465 -p 465:465 e71 bash -c 'mono testimap.exe $LOCAL_PORT $HOST $PORT ./mail.pfx'

# start pop3 proxy
docker run -v /root/mail.pfx:/usr/src/app/build/mail.pfx:ro -d --restart=always --name=e71-pop -e LOCAL_PORT=995 -e HOST=pop.gmail.com -e PORT=995 -p 995:995 e71 bash -c 'mono testimap.exe $LOCAL_PORT $HOST $PORT ./mail.pfx'
```

## Stop or remove application

```sh
$ docker rm -f e71-imap
$ docker rm -f e71-pop
$ docker rm -f e71-smtp
```

## Setup mail on your device/phone

- Imap server: [your server ip address], port [993], protocol [SSL]
- Smtp server: [your server ip address], port [465], protocol [SSL]
- Pop3 server: [your server ip address], port [995], protocol [SSL]


**This software is free and opensource. You can use it as you want, no fees or limits!**
