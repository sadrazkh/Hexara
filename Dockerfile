# ── مرحله ۱: build دارایی‌های فرانت با ویت ──────────────────────────────
FROM node:22-alpine AS client
WORKDIR /client
COPY src/Hexara.Web/ClientApp/package*.json ./
RUN npm ci
COPY src/Hexara.Web/ClientApp/ ./
# فایل‌های ترجمه بیرون از ClientApp هستند ولی با alias@locales وارد می‌شوند.
COPY src/Hexara.Web/Locales/ ../Locales/
RUN npm run build

# ── مرحله ۲: build و publish بک‌اند ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS server
WORKDIR /src
COPY Hexara.slnx ./
COPY src/Hexara.Domain/*.csproj src/Hexara.Domain/
COPY src/Hexara.Application/*.csproj src/Hexara.Application/
COPY src/Hexara.Infrastructure/*.csproj src/Hexara.Infrastructure/
COPY src/Hexara.Web/*.csproj src/Hexara.Web/
COPY tests/Hexara.Domain.Tests/*.csproj tests/Hexara.Domain.Tests/
RUN dotnet restore src/Hexara.Web/Hexara.Web.csproj

COPY . .
COPY --from=client /wwwroot/dist src/Hexara.Web/wwwroot/dist
# سرویس‌ورکر عمداً بیرون از dist ساخته می‌شود تا دامنه‌اش کل سایت باشد، پس جدا کپی می‌شود.
COPY --from=client /wwwroot/sw.js src/Hexara.Web/wwwroot/sw.js
RUN dotnet publish src/Hexara.Web/Hexara.Web.csproj -c Release -o /app --no-restore

# ── مرحله ۳: اجرا ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=server /app ./
ENTRYPOINT ["dotnet", "Hexara.Web.dll"]
