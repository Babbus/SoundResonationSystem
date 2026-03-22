# Sistem Mimarisi — Ses Rezonans Simülasyonu

## Genel Bakış (Ağaç Yapısı)

```
SoundResonance/
│
├── Runtime/
│   │
│   ├── ScriptableObjects/              [VERİ KATMANI — Malzeme Tanımları]
│   │   ├── BlittableMaterialData       Malzemenin fiziksel özelliklerini tutan struct
│   │   ├── MaterialProfileSO           Unity editörü için malzeme dosyası (ScriptableObject)
│   │   └── MaterialDatabase            Tüm malzemeleri bir arada tutan veritabanı
│   │
│   ├── Physics/                        [FİZİK KATMANI — Hesaplamalar]
│   │   ├── ShapeClassifier             Nesnenin şeklini belirler (çubuk/plaka/kabuk)
│   │   ├── FrequencyCalculator         Şekil + malzemeden doğal frekansı hesaplar
│   │   ├── ResonanceMath               Rezonans fiziğinin tüm matematiği (Lorentz, sönüm, mesafe)
│   │   └── NoteNameHelper              Frekans değerini müzik notasına çevirir (örn: 440 Hz → A4)
│   │
│   ├── Components/                     [ECS BİLEŞENLERİ — Varlık Verileri]
│   │   ├── ResonantObjectData          Her nesnenin frekans, Q-faktör ve genlik bilgisi
│   │   ├── EmitterTag                  "Bu nesne şu an titreşim yapıyor" işaretçisi
│   │   └── StrikeEvent                 "Bu nesneye vuruldu" tek seferlik olay bilgisi
│   │
│   └── Authoring/                      [DÖNÜŞÜM KATMANI — GameObject'ten ECS'ye]
│       └── ResonantObjectAuthoring     Tasarımcının sahneye yerleştirdiği bileşeni ECS verisine çevirir
│
└── Editor/                             [EDİTÖR ARAÇLARI — Sadece geliştirme sırasında çalışır]
    └── Inspectors/
        ├── ResonantObjectAuthoringEditor   Inspector panelinde frekansı ve notayı canlı gösterir
        └── MaterialPresetGenerator         Hazır malzeme dosyalarını tek tuşla oluşturur
```


## Katman Katman Açıklama

### 1. Veri Katmanı (ScriptableObjects)

**BlittableMaterialData** — Malzeme struct'ı
- Burst/Jobs ile çalışabilecek saf veri yapısı
- İçerir: Young modülü, yoğunluk, kayıp faktörü, Poisson oranı
- Bunlardan türetilir: Q-faktör (= 1/kayıp faktörü), ses hızı (= √(E/ρ))

**MaterialProfileSO** — Malzeme profil dosyası
- Unity editörü için ScriptableObject sarmalayıcısı
- Tasarımcı bu dosyayı seçer (örneğin "Çelik", "Cam", "Ahşap")
- İçerideki değerler gerçek malzeme bilimi verilerinden alınmıştır

**MaterialDatabase** — Malzeme veritabanı
- Tüm malzeme profillerini bir listede tutar
- 10 hazır malzeme için varsayılan değerler içerir
  (Çelik, Alüminyum, Cam, Pirinç, Bakır, Meşe, Ladin, Beton, Kauçuk, Seramik)


### 2. Fizik Katmanı (Physics)

**ShapeClassifier** — Şekil sınıflandırıcı
- Nesnenin sınırlayıcı kutusuna (bounding box) bakar
- Boyut oranlarını karşılaştırır
- Üç kategoriden birine atar:
  - Çubuk (Bar): Bir boyut diğer ikisinden çok büyükse (örn: diyapazon kolu)
  - Plaka (Plate): İki boyut büyük, biri ince, düz (örn: zil, panel)
  - Kabuk (Shell): İki boyut büyük, biri ince, eğimli (örn: çan, kâse)
- Çıktı: şekil tipi + karakteristik uzunluk + kalınlık

**FrequencyCalculator** — Frekans hesaplayıcı
- Şekil tipine göre doğru formülü seçer
- Çubuk: Euler-Bernoulli kiriş teorisi (f ~ kalınlık / uzunluk²)
- Plaka: Kirchhoff plaka teorisi (f ~ kalınlık / çap²)
- Kabuk: Donnell kabuk teorisi (f ~ et kalınlığı / yarıçap²)
- Ortak ilke: sert malzeme → yüksek frekans, ağır malzeme → düşük frekans

**ResonanceMath** — Rezonans matematiği
- Sistemin kalbi — tüm fizik hesaplamaları burada
- Beş temel fonksiyon:
  1. LorentzianResponse: Kaynak frekansı alıcının doğal frekansına ne kadar yakınsa, yanıt o kadar güçlü
  2. DriveTimeConstant: Sürülen bir nesnenin kararlı duruma ulaşma süresi
  3. ExponentialDecay: Kaynak kesildiğinde genliğin üstel olarak azalması
  4. InverseSquareAttenuation: Mesafe iki katına çıkarsa güç dört katına düşer
  5. DrivenOscillatorStep: Kare kare genlik güncellemesi (frame-rate bağımsız)

**NoteNameHelper** — Nota dönüştürücü
- Hz değerini en yakın müzik notasına çevirir
- Örnek: 440 Hz → "A4 +0c", 261.6 Hz → "C4 +0c"
- Editörde tasarımcıya anlamlı geri bildirim sağlar


### 3. ECS Bileşenleri (Components)

**ResonantObjectData** — Nesne verisi
- Her titreşebilen nesnenin ana bilgilerini tutar
- Bake sırasında yazılır: doğal frekans, Q-faktör, şekil
- Çalışma zamanında güncellenir: güncel genlik, faz

**EmitterTag** — Yayıcı etiketi
- IEnableableComponent: açma/kapama maliyetsiz (yapısal değişiklik yok)
- Nesneye vurulunca açılır, genlik eşik altına düşünce kapanır
- Sadece açık olan nesneler işlem görür (performans için)

**StrikeEvent** — Vuruş olayı
- Tek seferlik sinyal: "bu nesneye X kuvvetle vuruldu"
- Sistem okur, işler, sonra kapatır
- NormalizedForce [0-1]: 1.0 = sert vuruş, 0.1 = hafif dokunma


### 4. Dönüşüm Katmanı (Authoring)

**ResonantObjectAuthoring** — Yazarlama bileşeni
- Tasarımcı bunu sahneye koyar, bir malzeme profili seçer
- Baker (editörde çalışır):
  1. MeshFilter'dan sınırlayıcı kutuyu okur
  2. ShapeClassifier ile şekli belirler
  3. FrequencyCalculator ile frekansı hesaplar
  4. Sonucu ResonantObjectData olarak ECS'ye yazar
  5. EmitterTag ve StrikeEvent ekler (kapalı durumda)
- Tüm hesaplamalar editörde bir kez yapılır, runtime'da tekrar hesaplanmaz


### 5. Editör Araçları

**ResonantObjectAuthoringEditor** — Özel inspector paneli
- Malzeme veya boyut değiştiğinde anında güncellenir
- Gösterir: şekil tipi, frekans (Hz), müzik notası, Q-faktör
- Tasarımcı boyutu büyütünce frekansın düştüğünü canlı görebilir

**MaterialPresetGenerator** — Malzeme oluşturucu
- Menü: Sound Resonance → Generate Material Presets
- 10 hazır malzemeyi otomatik oluşturur
- Her birini ayrı .asset dosyası olarak kaydeder


## Veri Akışı

```
[Editörde — bir kez]

  MaterialProfileSO ──→ BlittableMaterialData
         │                       │
         │                       ▼
  MeshFilter.bounds ──→ ShapeClassifier ──→ FrequencyCalculator
                                                    │
                                                    ▼
                                           ResonantObjectData (ECS)
                                           + EmitterTag (kapalı)
                                           + StrikeEvent (kapalı)


[Çalışma zamanında — her kare]

  Vuruş Girdisi ──→ StrikeEvent (aç) ──→ EmitterTag (aç)
                                              │
                                              ▼
  Her kaynak-alıcı çifti için:
  ├─ LorentzianResponse(kaynak frekansı, alıcı frekansı, Q)
  ├─ InverseSquareAttenuation(mesafe)
  ├─ DrivenOscillatorStep(genlik, hedef, dt, f0, Q)
  └─ veya ExponentialDecay(genlik, süre, f0, Q)
                    │
                    ▼
           ResonantObjectData.CurrentAmplitude güncellenir
                    │
                    ▼
            Ses Çıktısı (henüz uygulanmadı)
```
