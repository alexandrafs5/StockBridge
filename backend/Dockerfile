FROM node:20-alpine

# Prisma necesita OpenSSL para su motor de queries — Alpine no lo trae por default.
RUN apk add --no-cache openssl

WORKDIR /app

COPY package*.json ./
RUN npm install --omit=dev

COPY prisma ./prisma
RUN npx prisma generate

COPY src ./src

EXPOSE 3000

CMD ["node", "src/index.js"]
