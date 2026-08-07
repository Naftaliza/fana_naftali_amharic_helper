# Build and run the Next.js frontend.
FROM node:20-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm install

FROM node:20-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
ARG NEXT_PUBLIC_API_URL=http://localhost:5080
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
# Feature flag, off by default — see frontend/lib/featureFlags.ts. NEXT_PUBLIC_* vars are
# inlined at build time, so this must be a build ARG, not a container-runtime env var.
ARG NEXT_PUBLIC_FEATURE_WALLET=false
ENV NEXT_PUBLIC_FEATURE_WALLET=$NEXT_PUBLIC_FEATURE_WALLET
RUN npm run build

FROM node:20-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
COPY --from=build /app/.next ./.next
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/package.json ./package.json
COPY --from=build /app/public ./public
EXPOSE 3000
CMD ["npm", "start"]
