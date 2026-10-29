# Steam'e yükleme (SteamPipe)

`app_build_4559890.vdf` iki depot'u birlikte yükler:

| Depot | Klasör | Menü |
|---|---|---|
| 4559891 (Windows) | `Builds/Windows/` | Tools ▸ RogueBlockBlast ▸ Build ▸ Windows (x64) |
| 4559892 (macOS) | `Builds/macOS/Blockflow.app` | Tools ▸ RogueBlockBlast ▸ Build ▸ macOS (Intel + Apple Silicon) |

Her iki build de yükleme öncesinde alınmış olmalı. Bir build'i yüklemeden bırakmak o depot'u boş gönderir.

## Steamworks'te bir kez yapılacaklar

1. **Depot ID'leri.** SteamPipe ▸ Depots'ta Windows depot'unun ID'si 4559891 değilse, hem `app_build_4559890.vdf`'te hem `depot_build_windows.vdf`'te düzelt.
2. **Mac depot'u.** Aynı sayfada *Add New Depot* ile bir depot ekle ve Configuration ▸ Operating System: **macOS** seç. Verilen ID 4559892 değilse iki dosyada da düzelt. Windows depot'unun OS'u **Windows** olmalı. "All OSes" olursa Mac kullanıcısı Windows dosyalarını da indirir.
3. **Launch option.** Installation ▸ General ▸ *Add new launch option*: Executable `Blockflow.app`, Operating System **macOS**. Var olan Windows satırının OS'u **Windows** olmalı.
4. **Steam Cloud.** Steam Cloud ▸ Root Overrides: Original root `WinAppDataLocalLow`, OS **macOS**, New root **MacAppSupport**. Path değişmez: `Designer Dad Games/Blockflow` (Unity Mac'te `~/Library/Application Support/Designer Dad Games/Blockflow/save.json` yazar).
5. **Packages.** Mac depot'unu satış paketine (ve varsa dev/beta paketlerine) ekle.
6. Mağaza sayfasında macOS'u işaretle, sistem gereksinimlerini yaz (macOS 12+, Intel veya Apple Silicon).
7. Hepsini **Publish** et.

## Yükleme

[steamcmd](https://developer.valvesoftware.com/wiki/SteamCMD) ile, proje kökünden:

```bash
steamcmd +login <steam_kullanıcı_adı> +run_app_build "%CD%\Steam\SteamPipe\app_build_4559890.vdf" +quit
```

`SetLive` boş olduğu için build hiçbir branch'te yayınlanmaz. Steamworks ▸ Builds'de önce bir beta branch'e (ör. `mac-test`) al, Mac'te Steam üzerinden test et, sonra default'a al.

## Mac'te test

- **Steam üzerinden (asıl test):** Steam ▸ Blockflow ▸ Properties ▸ Betas'tan branch'i seç ve oyunu kur. Achievement'lar, Steam Cloud ve controller tipi ancak böyle test edilir.
- **Steam olmadan (`.app`'i elle kopyalayarak):** Windows'ta alınan build'de çalıştırma izni ve indirme karantinası sorun çıkarır. Mac'te:

  ```bash
  chmod +x Blockflow.app/Contents/MacOS/Blockflow
  ```

  ```bash
  xattr -cr Blockflow.app
  ```

  Steam açıkken ve hesapta oyun varken çalıştırmak için `steam_appid.txt` (içinde `4559890`) dosyasını bulunduğun klasöre koy ve terminalden başlat: `./Blockflow.app/Contents/MacOS/Blockflow`. Bu dosya olmadan oyun kendini Steam üzerinden yeniden başlatır.
- Log: `~/Library/Logs/Designer Dad Games/Blockflow/Player.log`

Steam'den kurulan oyun açılmıyor ve "hasarlı / açılamıyor" deniyorsa, sebep büyük ihtimalle Windows'tan yüklenen dosyada çalıştırma izninin olmamasıdır. Bu durumda Mac depot'unu bir Mac'ten yükle.
