# BalanceSim

Ekonomi ve zorluk simülatörü. `RunController.DoPlace`, `ComboSystem`, `MilestoneSystem`, `CardEffectApplier` ve kart seçim ekranının birebir portu; veriyi doğrudan `Assets/GameData` asset'lerinden okur, yani asset'te bir değer değişince yeniden derlemeye gerek yok.

Unity'nin kendi Roslyn derleyicisi ve .NET 6 runtime'ı ile çalışır, ayrı .NET SDK gerekmez.

```bash
./build.sh
./run.sh baseline 4000        # yükseltmesiz zorluk eğrisi
./run.sh cards 1500           # kart etki analizi (bot kart seçimini bundan öğrenir)
./run.sh invest 1500          # upgrade / şekil açma / skor seviyesi getirisi
./run.sh meta 200 3000        # tam oyun meta ilerleme (persona başına 200 oyuncu)
./run.sh meta 200 400 demo    # demo (Stage 6'da biten) meta ilerleme
./run.sh bugs 4000            # yeni-havuz revive hatasının etkisi
./run.sh trace Mid 1          # tek oyuncunun run run dökümü
./run.sh fidelity 8           # Unity'de gerçek sınıflarla tekrar oynatmak için hamle kaydı
./run.sh export               # out/report_data.json → report/template.html
./run.sh classic 3000         # Classic Mode: tek oyun skor/satır/süre dağılımı (başarım eşikleri)
./run.sh classic 1000 rotate  # karşılaştırma varyantları: rotate (döndürme açık), all (kilitli şekiller de), nofit (yeniden çekme yok)
```

`classic` oyundaki Classic kurallarını modeller (`GameMode.Classic`): milestone/kart/upgrade yok, döndürme kapalı, parça rastgele yönde gelir, yalnızca başta açık şekiller, sığmayan parça 2 kez yeniden çekilir. Çıktı `out/classic.json`; eşik başına "tek oyunda ulaşma %" `AchievementTracker`'daki Classic eşiklerinin kaynağıdır.

Rapor: `report/template.html` içindeki `/*__DATA__*/null` yerine `out/report_data.json` konarak `report.html` üretilir.

Motor oyunun güncel kodunu modelliyor. `Config.Legacy = true`, 23 Ekim 2026 düzeltmelerinden önceki davranışı geri getirir (ölü yeni havuzda revive atlanır, card reroll kullanılamaz, Shape_4gen/L kartları çıkmaz); `bugs` deneyi ikisini karşılaştırır. Düzeltme öncesi sonuçlar `out/before/` altında.
