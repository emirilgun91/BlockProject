# Trailer sistemi

Ekrandaki her değer (puan, milestone eşiği, kalan şekil, eldeki kart, combo) gerçek bir run'dan gelir: hiçbir sayı elle yazılmaz.

## Parçalar

| Dosya | Ne yapar |
|---|---|
| `Assets/Scripts/Game/Trailer/TrailerDirector.cs` | Oyunu kendi oynar. Sanal fare tahtaya gider, ghost görünür, tıklama gerçek `DoPlace` yolundan geçer. Kart seçimi fiziksel kartın SELECT butonuna tıklanarak yapılır. Planlayıcı combo şarjını ve çarpanı hesaba katar. |
| `Assets/Scripts/Editor/TrailerRecorderBridge.cs` | Take başlayınca Unity Recorder'ı açar: `Takes/<isim>.mp4`, 1920×1080, 60 fps sabit, 32 Mbps, sesli (müziksiz). |
| `edl.sh` | Kurgu listesi: hangi take'in hangi saniyesi, ne kadar, üstte hangi yazı. |
| `build.sh` | `edl.sh`'ı okuyup `Out/Blockflow_Trailer.mp4`'ü üretir: kesimler, yazılar, logo kartları ve tek parça `Gameplay_Loop` müziği. |
| `sheet.sh` | Bir videodan zaman damgalı kare dizisi çıkarır (kesim noktası seçmek için). |

## Yeni take çekmek

1. **Tools ▸ RogueBlockBlast ▸ Trailer ▸ Begin Session**: save.json yedeklenir, steam_appid.txt kenara alınır.
2. Play mode'da (SampleScene, Game view 1920×1080) MCP veya bir editör script'i ile:
   ```
   TrailerDirector.Begin("{\"Name\":\"E_new\",\"Seed\":7,\"FastForwardMilestones\":3,\"RecordMilestones\":2}")
   ```
   - `FastForwardMilestones`: kayıt başlamadan görünmeden kaç milestone geçilsin. Eldeki kart sayısı bu sayıya eşit olur.
   - `RecordMilestones` / `MaxRecordSeconds`: kayıt ne zaman bitsin.
   - `CardPlan`: sunulursa sırayla seçilecek kart Id'leri. `Spectacle`: kayıtta çok-satırlı temizlemeye ne kadar oynasın.
   - İlerleme: `TrailerDirector.LastReport` (her kart seçimi ve kayıt başı/sonu durumu: puan, eşik, kalan şekil, kart sayısı).
3. Play mode'dan çık → **End Session**: save ve steam_appid geri gelir.

İleri sarma bazı seed'lerde ölebilir ("FF sırasında oyun bitti"). Başka bir seed dene.

## Kurguyu yeniden üretmek

```bash
bash Trailer/build.sh
```

`edl.sh`'daki satırları değiştirip tekrar çalıştırmak yeterli. ffmpeg winget ile kurulu (`Gyan.FFmpeg`); başka bir yerdeyse `FFMPEG_DIR` ortam değişkeniyle belirt.
