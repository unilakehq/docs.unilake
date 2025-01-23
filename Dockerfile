FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ENV custom_domain=localhost

RUN apt update && \
    apt install jq nodejs npm -y && \
    apt clean

COPY . .

WORKDIR /src

RUN npm install \
    dotnet dev-certs https && \
    npm run prerender

CMD [ "npm", "run", "serve" ]