using System.Globalization;
using System.Text;

namespace LoanPaymentCalculator;

// Extracted calculation: arithmetic and grace-period behavior are intentionally preserved.
// Company enums are replaced with period labels; holidays are supplied by the caller.
public sealed class OriginalCalculator(IEnumerable<DateOnly>? holidays = null)
{
    private readonly DateOnly[] holidayDates = holidays?.Distinct().ToArray() ?? [];
        public string ItfaPlanHesapla(DateTime? krediKullandirmaTarih, decimal? krediTutar, int? vade, int? anaParaOdemesizSure, int? anaParaOdemePeriyot,
    string vadePeriyot, string faizVade, decimal? faizOran, decimal? KKDF, decimal? BSMV, int? faizOdemesizSure, string ayrac, bool? tatilGunleriniOtele)
        {
            if (krediTutar is null || vade is null || anaParaOdemePeriyot is null || faizOdemesizSure is null)
                return "Tutar, vade, ödeme aralığı ve faiz ödemesiz süre gereklidir.";
            if (anaParaOdemePeriyot <= 0 || vade <= 0 || krediTutar <= 0)
                return "Tutar, vade ve ödeme aralığı sıfırdan büyük olmalı.";
            // Yapılan işlemlerin hepsinde anaParaOdemePeriyot aynı zamanda faizin ödeme periyodu olarak kabul edilmiştir
            // Faiz ve anaparanın vadeleri aynı olduğundan girilen faiz anaparanın vadesine çevrilmiştir

            #region Hesaplama koşulları
            if (ayrac == string.Empty) return "Kuruş ayracını seçiniz.";
            if (faizOran == null) return "Kredi faiz oranını belirtiniz.";
            if (krediKullandirmaTarih == null) return "Kredi kullandırma tarihini belirtiniz.";
            if (anaParaOdemesizSure == null) return "Ödemesiz süreyi belirtiniz.";
            if(tatilGunleriniOtele == null) return "Ödemelerde tatil günleri ötelenmeli mi belirtiniz.";
            if ((vade - anaParaOdemesizSure) % anaParaOdemePeriyot != 0)
                return $"Ödemesiz süre sonrası kalan vade ({vade.Value - anaParaOdemesizSure.Value}), ödeme periyoduna ({anaParaOdemePeriyot.Value}) tam bölünmüyor. Vade veya ödemesiz süreyi kontrol ediniz.";
            if (vadePeriyot != "Gün"
                && vadePeriyot != "Ay"
                && vadePeriyot != "Yıl") return "Vade periyodunu seçiniz.";
            if (faizOdemesizSure > anaParaOdemesizSure) return "Faiz ödemesiz süresi ana para ödemesiz süresinden küçük olmalı.";
            if(anaParaOdemesizSure >= vade) return "Ana paranın ödemesiz süresi vadeden büyük veya eşit olamaz";
            if (((anaParaOdemesizSure - faizOdemesizSure) % anaParaOdemePeriyot) != 0)
            {
                var sadeceFaizOdenenDonem = anaParaOdemesizSure.Value - (faizOdemesizSure ?? 0);
                return $"Ana para ödemesiz süresi ({anaParaOdemesizSure.Value}) ile faiz ödemesiz süresi ({faizOdemesizSure ?? 0}) arasındaki {sadeceFaizOdenenDonem} birimlik dönemde faiz ödemesi beklenir; ancak bu hesaplamada faiz ve anapara ödemeleri aynı periyotta ({anaParaOdemePeriyot.Value} birim) kabul edildiğinden bu dönem periyoda uymuyor. Tam ödemesiz dönem isteniyorsa faiz ödemesiz süresini ana para ödemesiz süresiyle aynı giriniz.";
            }

            #endregion
            #region Efektif faiz hesabı
            const int YildakiAySayisi = 12;
            const int YildakiGunSayisi = 365;

            const decimal YuzdeBoleni = 100m;

            decimal vergisizFaizOrani = 0.00m;

            if (vadePeriyot == "Yıl")
            {
                vergisizFaizOrani = faizOran.Value / YuzdeBoleni;
            }
            else if (vadePeriyot == "Ay")
            {
                vergisizFaizOrani = faizOran.Value / (YildakiAySayisi * YuzdeBoleni);
            }
            else if (vadePeriyot == "Gün")
            {
                vergisizFaizOrani = faizOran.Value / (YildakiGunSayisi * YuzdeBoleni);
            }
            #endregion

            // Vergili faiz oranı hesabı
            decimal kkdfOrani = 0.00m;
            decimal bsmvOrani = 0.00m;
            if (KKDF.HasValue) kkdfOrani = KKDF.Value / YuzdeBoleni;
            if (BSMV.HasValue) bsmvOrani = BSMV.Value / YuzdeBoleni;

            var odemeTarih = krediKullandirmaTarih;
            var vergiliFaizOrani = vergisizFaizOrani * (1 + kkdfOrani + bsmvOrani);

            // Vergili ve vergisiz faiz oranının aylık verileri hesaplanmıştır, ödeme periyodu 1'den büyükse bu faizler ödeme periyoduna göre efektif hallerine çevrilir
            if (anaParaOdemePeriyot > 1)
            {
                vergisizFaizOrani = DecimalPow(1 + vergisizFaizOrani, anaParaOdemePeriyot.Value) - 1;
                vergiliFaizOrani = DecimalPow(1 + vergiliFaizOrani, anaParaOdemePeriyot.Value) - 1;
            }

            var kalanBorc = krediTutar.Value;
            var kalanBorcVergisiz = krediTutar.Value;
            var birikenFaiz = 0.00m;
            var birikenFaizVergisiz = 0.00m;
            var odemePlani = new List<OdemeSatiri>();

            var sadeceFaizOdenenSure = (anaParaOdemesizSure - faizOdemesizSure) / anaParaOdemePeriyot;

            int odemePeriyotSayisi = ((vade.Value - anaParaOdemesizSure.Value) / anaParaOdemePeriyot.Value);

            // Ödemesiz sürede binecek faizi hesaplar
            if (faizOdemesizSure > 0)
            {
                for (int i = 0; i <= (faizOdemesizSure / anaParaOdemePeriyot); i++)
                {
                    if (i > 0)
                    {
                        odemeTarih = odemeTarihHesapla(odemeTarih, vadePeriyot, anaParaOdemePeriyot);
                    }

                    var tempKalanBorc = kalanBorc;
                    kalanBorc += tempKalanBorc * vergiliFaizOrani;
                    kalanBorcVergisiz += tempKalanBorc * vergisizFaizOrani; // Faiz ödemesiz süre varsa ilk ödemede kullanılır
                    birikenFaiz = kalanBorc - krediTutar.Value;
                    birikenFaizVergisiz = kalanBorcVergisiz - krediTutar.Value; // Sadece faiz ödenen sürede kullanılır
                }
            }

            // Sadece faiz ödenen süredeki veri girişleri
            for (int i = 1; i <= sadeceFaizOdenenSure; i++)
            {
                odemeTarih = odemeTarihHesapla(odemeTarih, vadePeriyot, anaParaOdemePeriyot);

                if (i == 1 && birikenFaiz > 0)
                    kalanBorc -= birikenFaiz;

                odemePlani.Add(new OdemeSatiri
                {
                    Tarih = odemeTarih.Value,
                    TaksitNo = (i).ToString(),
                    TaksitMiktari = i == 1 && birikenFaiz > 0 ? birikenFaiz : kalanBorc * vergiliFaizOrani,
                    Anapara = 0,
                    Faiz = i == 1 && birikenFaiz > 0 ? birikenFaizVergisiz : kalanBorc * vergisizFaizOrani,
                    KalanPara = kalanBorc
                });

                if (i == 1)
                {
                    birikenFaiz = 0;
                }
            }

            // Eğer kredide faiz ödemesiz süre varsa ilk ödemede biriken bütün faiz kapatılır
            var butunFaizOdendiMi = false;
            if (kalanBorc > krediTutar && odemePeriyotSayisi > 1)
            {
                odemeTarih = odemeTarihHesapla(odemeTarih, vadePeriyot, anaParaOdemePeriyot);

                var taksitNo = 1;
                if (sadeceFaizOdenenSure.HasValue)
                {
                    sadeceFaizOdenenSure += 1;
                    taksitNo = sadeceFaizOdenenSure.Value;
                }

                odemePlani.Add(new OdemeSatiri
                {
                    Tarih = odemeTarih.Value,
                    TaksitNo = (sadeceFaizOdenenSure.Value).ToString(),
                    TaksitMiktari = kalanBorc - krediTutar.Value,
                    Anapara = 0,
                    Faiz = kalanBorcVergisiz - krediTutar.Value,
                    KalanPara = krediTutar.Value
                });

                kalanBorc = krediTutar.Value;
                butunFaizOdendiMi = true;
                birikenFaiz = 0;
            }

            // Kalan borç miktarına göre aylık ödenecek taksit tutarını hesaplar
            if (butunFaizOdendiMi) // Fazladan ödeme yapıldıysa ödenecek tutarları buna göre hesaplar
            {
                odemePeriyotSayisi = ((vade.Value - anaParaOdemesizSure.Value) / anaParaOdemePeriyot.Value) -1;
                if (odemePeriyotSayisi == 0)
                    return "Girilen parametrelerle ana para taksit sayısı sıfır; vade, ödemesiz süre ve ödeme periyodunu kontrol ediniz.";
            }

            decimal taksit;
            if (vergiliFaizOrani > 0)
            {
                decimal factor = DecimalPow(1 + vergiliFaizOrani, odemePeriyotSayisi);
                taksit = kalanBorc * vergiliFaizOrani * factor / (factor - 1);
            }
            else
            {
                taksit = kalanBorc / odemePeriyotSayisi; // 0 faiz için
            }

            taksit = Math.Round(taksit, 2); // Tutarlı hesaplamalar için bütün taksitleri 2 haneliye yuvarlar

            // Stringe yazılacak ödemeler
            for (int i = 1; i <= odemePeriyotSayisi; i++)
            {
                odemeTarih = odemeTarihHesapla(odemeTarih, vadePeriyot, anaParaOdemePeriyot);

                var vergiliFaizTutari = kalanBorc * vergiliFaizOrani;
                var vergisizFaizTutari = kalanBorc * vergisizFaizOrani;

                vergiliFaizTutari = Math.Round(vergiliFaizTutari, 2);
                vergisizFaizTutari = Math.Round(vergisizFaizTutari, 2);

                decimal anaParaOdemesi;
                decimal taksitBuAy;

                if (i == odemePeriyotSayisi)
                {
                    anaParaOdemesi = kalanBorc;
                    taksitBuAy = anaParaOdemesi + vergiliFaizTutari; // Son taksit
                }
                else
                {
                    taksitBuAy = taksit;
                    anaParaOdemesi = taksitBuAy - vergiliFaizTutari;
                }

                kalanBorc -= anaParaOdemesi;

                odemePlani.Add(new OdemeSatiri
                {
                    Tarih = odemeTarih.Value,
                    TaksitNo = (i + sadeceFaizOdenenSure.Value).ToString(),
                    TaksitMiktari = i == 1 && birikenFaiz > 0 ? birikenFaiz + krediTutar.Value : taksitBuAy,
                    Anapara = i == 1 && birikenFaiz > 0 ? krediTutar.Value : anaParaOdemesi,
                    Faiz = i == 1 && birikenFaiz > 0 ? birikenFaizVergisiz : vergisizFaizTutari,
                    KalanPara = kalanBorc < 0.01m ? 0 : kalanBorc
                });
            }

            var sb = new StringBuilder();
            var cultureInfo = new CultureInfo("tr-TR");
            if (ayrac == ".")
            {
                cultureInfo = new CultureInfo("en-GB");
            }

            if (tatilGunleriniOtele ?? false)
            {
                var resmiTatilListe = tatilTarihGetir(krediKullandirmaTarih.Value, vade ?? 0, vadePeriyot).ToList();
                var resmiTatilSet = new HashSet<DateTime>(resmiTatilListe.Select(t => t.Tarih.Date));
                foreach (var satir in odemePlani)
                {
                    DateTime normalOdemeTarih = satir.Tarih;
                    DateTime isGunundeOdemeTarih = odemeIsGununeErtele(normalOdemeTarih, resmiTatilSet);
                    if (normalOdemeTarih != isGunundeOdemeTarih)
                    {
                        satir.Tarih = isGunundeOdemeTarih;
                    }
                }
            }

            foreach (var satir in odemePlani)
            {
                sb.AppendLine(string.Join("\t",
                    satir.Tarih.ToString("dd.MM.yyyy"), satir.TaksitNo,
                    satir.TaksitMiktari.ToString("N2", cultureInfo),
                    satir.Anapara.ToString("N2", cultureInfo),
                    satir.Faiz.ToString("N2", cultureInfo),
                    satir.KalanPara.ToString("N2", cultureInfo)
                ));
            }
            return sb.ToString();
        }


    private static decimal DecimalPow(decimal value, int exponent)
    {
        decimal result = 1;
        for (int i = 0; i < exponent; i++) result *= value;
        return result;
    }

    private static DateTime odemeTarihHesapla(DateTime? date, string period, int? interval) => period switch
    {
        "Gün" => date!.Value.AddDays(interval!.Value),
        "Ay" => date!.Value.AddMonths(interval!.Value),
        "Yıl" => date!.Value.AddYears(interval!.Value),
        _ => date!.Value
    };

    private IEnumerable<TatilTarih> tatilTarihGetir(DateTime start, int term, string period)
    {
        // Include dates after nominal maturity: a shifted payment can land on a later holiday.
        return holidayDates
            .Select(date => new TatilTarih { Tarih = date.ToDateTime(TimeOnly.MinValue) });
    }

    private static DateTime odemeIsGununeErtele(DateTime date, HashSet<DateTime> holidaySet)
    {
        while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday || holidaySet.Contains(date.Date))
            date = date.AddDays(1);
        return date;
    }

    private sealed class TatilTarih { public DateTime Tarih { get; init; } }
    private sealed class OdemeSatiri
    {
        public DateTime Tarih { get; set; }
        public string TaksitNo { get; init; } = "";
        public decimal TaksitMiktari { get; init; }
        public decimal Anapara { get; init; }
        public decimal Faiz { get; init; }
        public decimal KalanPara { get; init; }
    }
}
