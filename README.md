# AnimeStreamingApi

ASP.NET Core REST API theo Clean Architecture, proxy tới [AniMapper](https://animapper.net/docs/api/api-overview) (`https://api.animapper.net/api/v1`).

## Chạy

Yêu cầu .NET 8 SDK.

```bash
dotnet restore
dotnet run --project src/Anime.Api
```

Swagger (môi trường Development): `http://localhost:5000/swagger`

## Endpoints

| Nhóm | Endpoint | Mô tả |
|------|----------|-------|
| Search | `GET /api/v1/search?title=attack&mediaType=ANIME&limit=10` | Tìm kiếm với bộ lọc nâng cao (chỉ anime có nguồn phát, trang sau dùng `offset = nextOffset`) |
| Browse | `GET /api/v1/browse?tab=SEASON&season=SUMMER&seasonYear=2026&limit=24` | Danh sách theo tab `TRENDING`, `POPULAR`, `SEASON`, `NEW`, `AIRING`, `FINISHED`, `MOVIE`. Thứ tự lấy từ AniList GraphQL (cache 10 phút), chỉ giữ anime có nguồn phát |
| Autocomplete | `GET /api/v1/autocomplete?q=attack&mediaType=ANIME&locale=vi&limit=5` | Gợi ý tiêu đề (limit 1-10) |
| Autocomplete | `GET /api/v1/autocomplete/status` | Trạng thái dịch vụ autocomplete |
| Metadata | `GET /api/v1/metadata?id=16498` | Chi tiết đầy đủ một media |
| Metadata | `GET /api/v1/metadata/batch?ids=16498,11061,1535` | Metadata nhiều media |
| Streaming | `GET /api/v1/stream/episodes?id=16498&provider=ANIMEVIETSUB` | Danh sách tập |
| Streaming | `GET /api/v1/stream/episodes/servers?id=16498&provider=ANIMEVIETSUB` | Danh sách server |
| Streaming | `GET /api/v1/stream/source?episodeData=...&provider=ANIMEVIETSUB&server=DU` | Nguồn stream |
| Streaming | `GET /api/v1/stream/source/m3u8/{cacheKey}` | Playlist M3U8 |

### Format lỗi

Mọi lỗi đều trả về dạng:

```json
{ "success": false, "message": "Invalid season: SUMMR. Must be one of: WINTER, SPRING, SUMMER, FALL", "code": "INVALID_PARAMETER" }
```

| HTTP | code | Khi nào |
|------|------|---------|
| 400 | `MISSING_PARAMETER`, `INVALID_PARAMETER`, `INVALID_ID` | Tham số thiếu / sai (validate tại API trước khi gọi upstream) |
| 400/404/429 | giữ nguyên code của AniMapper (`MEDIA_NOT_FOUND`, `MAPPING_NOT_FOUND`, ...) | Lỗi 4xx từ AniMapper |
| 502 | `UPSTREAM_ERROR` | AniMapper lỗi 5xx / không kết nối được / JSON sai |
| 504 | `UPSTREAM_TIMEOUT` | AniMapper quá thời gian chờ |
| 503 | `PROVIDER_UNAVAILABLE` | Provider đang bị ngắt tạm do lỗi liên tục |
| 429 | `RATE_LIMITED` | Vượt hạn mức AniMapper, hàng chờ quá lâu |
| 500 | `INTERNAL_SERVER_ERROR` | Lỗi khác |

## Chống quá tải & tự phục hồi (AniMapper giới hạn 60 request/phút)

Nằm ở `src/Anime.Infrastructure/AniMapper/`, các service không cần biết:

| Thành phần | Tác dụng |
|---|---|
| `AniMapperRateGate` + `AniMapperRateLimitHandler` | Điều tiết request lên AniMapper (mặc định 50/phút, burst 10). Gặp 429 thì tạm dừng toàn bộ theo `Retry-After`; chờ lượt quá `MaxQueueWaitSeconds` thì trả 429 `RATE_LIMITED` |
| `AniMapperResponseCache` | Cache in-memory, gộp request trùng đang chạy. AniMapper lỗi tạm thời (timeout/5xx/429) thì trả **bản cũ** nếu có. Cache ngắn các lỗi 404 |
| `ProviderHealth` | Circuit breaker theo provider: 3 lỗi timeout/5xx trong 2 phút thì ngắt provider 60s, trả **503 `PROVIDER_UNAVAILABLE`** ngay để client chuyển nguồn |
| `CachingAniMapperClient` | Bọc `AniMapperClient`, áp các chính sách trên cho từng endpoint |

Thời gian cache (`AniMapperCachePolicies`): search/browse 5 phút, metadata 30 phút, servers 6 giờ, episodes 30 phút, source 20 phút, m3u8 30 phút. Bản dự phòng giữ 6 giờ – 2 ngày.
`GET /api/v1/stream/source?...&refresh=true` bỏ qua cache để lấy link mới khi link cũ đã hỏng.

### Lưu bền vững (PostgreSQL)

Ngoài RAM, response được lưu vào bảng `cached_responses` (`src/Anime.Infrastructure/Persistence/`):

- Bản lưu còn mới → trả luôn, không gọi AniMapper (kể cả sau khi BE khởi động lại).
- AniMapper/provider sập → trả bản lưu dù đã cũ.
- Thời gian giữ: metadata, servers, episodes 30 ngày; source, m3u8, search 7 ngày; autocomplete không lưu. Bản hết hạn được dọn mỗi 6 giờ.
- Migration tự chạy khi khởi động. DB không kết nối được thì API vẫn chạy (chỉ dùng cache RAM).

Connection string (`ConnectionStrings:AnimeDb`) đặt bằng user-secrets, không ghi vào `appsettings.json`:

```bash
dotnet user-secrets set "ConnectionStrings:AnimeDb" "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require" --project src/Anime.Api
```

Thêm migration (chạy được cả khi API đang chạy): `dotnet ef migrations add <Name> -p src/Anime.Infrastructure -s src/Anime.Infrastructure -o Persistence/Migrations`

## Admin (đăng nhập, đồng bộ, ẩn/hiện phim)

Trang quản trị ở FE: `/admin` (đăng nhập tại `/admin/login`).

| Endpoint | Mô tả |
|---|---|
| `POST /api/v1/admin/auth/login` | `{ username, password }` → JWT (giới hạn 10 lần/phút/IP) |
| `POST /api/v1/admin/auth/change-password` | `{ currentPassword, newPassword }` |
| `GET /api/v1/admin/stats` | Số phim, số có nguồn phát, số đang ẩn, các danh sách đã đồng bộ, job gần nhất |
| `GET /api/v1/admin/anime?q=&hasStream=&hidden=&list=&page=&pageSize=` | Danh mục phim |
| `PATCH /api/v1/admin/anime/{id}` | `{ isHidden: true }` ẩn / hiện phim |
| `POST /api/v1/admin/anime/{id}/sync` | Lấy mới 1 phim (thêm nếu chưa có) |
| `POST /api/v1/admin/sync` | `{ lists?: ["TRENDING", ...], updateExisting: false }` bắt đầu đồng bộ (chạy nền, 409 nếu đang chạy) |
| `GET /api/v1/admin/sync/jobs`, `/jobs/{id}` | Lịch sử / tiến độ đồng bộ |

- Đồng bộ **chỉ chạy khi admin bấm**, không chạy theo lịch. Thứ tự lấy từ AniList, metadata từ AniMapper.
- Phim đã có trong DB → **bỏ qua** (không gọi AniMapper, giữ nguyên trạng thái ẩn), kết quả báo `added` / `skipped`. Bật `updateExisting` để cập nhật lại.
- `/browse` đọc danh sách đã đồng bộ từ DB; danh sách chưa đồng bộ (vd mùa cũ) thì gọi AniList/AniMapper trực tiếp như trước.
- Phim bị ẩn bị loại khỏi `/browse`, `/search`, `/autocomplete`, `/metadata/batch`; `/metadata?id=` trả 404.

Secrets (user-secrets):

```bash
dotnet user-secrets set "Jwt:Key" "<chuỗi ngẫu nhiên ≥ 32 ký tự>" --project src/Anime.Api
dotnet user-secrets set "Admin:Username" "admin" --project src/Anime.Api
dotnet user-secrets set "Admin:Password" "<mật khẩu>" --project src/Anime.Api
```

Tài khoản admin chỉ được tạo khi khởi động lần đầu (DB chưa có admin). Sau đó đổi mật khẩu qua API/trang admin, sửa `Admin:Password` không còn tác dụng.

## Cấu hình

`src/Anime.Api/appsettings.json`:

```json
"AniMapper": {
  "BaseUrl": "https://api.animapper.net/",
  "TimeoutSeconds": 30,
  "UserAgent": "AnimeStreamingApi/1.0",
  "RequestsPerMinute": 50,
  "BurstSize": 10,
  "MaxQueueWaitSeconds": 15
}
```

## Kiến trúc

- `Anime.Api`: Controllers (mỏng), middleware xử lý lỗi, đăng ký DI (`Extensions/`)
- `Anime.Application`: DTOs, interfaces, services (validate + chuẩn hóa tham số), `Common/` (exceptions, error codes, `Guard`)
- `Anime.Infrastructure`: `AniMapperClient` (HttpClient typed), `AniMapperOptions`, `AddInfrastructure()`
- `Anime.Domain`: domain models

## Stream proxy

`GET /api/v1/stream/proxy?url={url}&h={base64url(JSON proxyHeaders)}`

Dùng khi `/stream/source` trả về `corsProxyRequired: true`. BE tải playlist/segment kèm header (vd `Referer`),
rewrite mọi link trong m3u8 để đi tiếp qua proxy, hỗ trợ `Range` (tua video). Chỉ cho phép URL http/https tới host public.

## Frontend

FE Angular là project riêng: `D:\Project\AnimeStreamingWeb` (gọi BE qua dev proxy `/api` → `http://localhost:5200`).
