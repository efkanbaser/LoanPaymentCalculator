        public string ItfaPlanHesapla(DateTime? krediKullandirmaTarih, decimal? krediTutar, int? vade, int? anaParaOdemesizSure, int? anaParaOdemePeriyot,
    string vadePeriyot, string faizVade, decimal? faizOran, decimal? KKDF, decimal? BSMV, int? faizOdemesizSure, string ayrac)
        {
            // Yapılan işlemlerin hepsinde anaParaOdemePeriyot aynı zamanda faizin ödeme periyodu olarak kabul edilmiştir
            // Faiz ve anaparanın vadeleri aynı olduğundan girilen faiz anaparanın vadesine çevrilmiştir

            #region Hesaplama koşulları
            if (faizOran == null)
            {
                return "Kredi faiz oranını belirtiniz.";
            }
            else if (krediKullandirmaTarih == null)
            {
                return "Kredi kullandırma tarihini belirtiniz.";
            }
            else if (anaParaOdemesizSure == null)
            {
                return "Ödemesiz süreyi belirtiniz.";
            }
            else if ((vade - anaParaOdemesizSure) % anaParaOdemePeriyot != 0)
            {
                return "Ödeme periyodunu kontrol ediniz.";
            }
            else if (vadePeriyot != VadePeriyotEnum.Gun.ToDescription()
                && vadePeriyot != VadePeriyotEnum.Ay.ToDescription()
                && vadePeriyot != VadePeriyotEnum.Yil.ToDescription())
            {
                return "Vade periyodunu seçiniz.";
            }
            else if (faizOdemesizSure > anaParaOdemesizSure)
            {
                return "Faiz ödemesiz süresi ana para ödemesiz süresinden küçük olmalı.";
            }
            else if(anaParaOdemesizSure >= vade)
            {
                return "Ana paranın ödemesiz süresi vadeden büyük veya eşit olamaz";
            }
            else if (((anaParaOdemesizSure - faizOdemesizSure) % anaParaOdemePeriyot) != 0)
            {
                return "Ödemesiz süreler, ödeme periyoduna uygun olmalı";
            }
            #endregion
            #region Efektif faiz hesabı
            const int YildakiAySayisi = 12;
            const int YildakiGunSayisi = 365; 

            const decimal YuzdeBoleni = 100m;

            decimal vergisizFaizOrani = 0.00m;

            if (vadePeriyot == VadePeriyotEnum.Yil.ToDescription()) 
            {
                vergisizFaizOrani = faizOran.Value / YuzdeBoleni;
            }
            else if (vadePeriyot == VadePeriyotEnum.Ay.ToDescription()) 
            {
                vergisizFaizOrani = faizOran.Value / (YildakiAySayisi * YuzdeBoleni);
            }
            else if (vadePeriyot == VadePeriyotEnum.Gun.ToDescription()) 
            {
                vergisizFaizOrani = faizOran.Value / (YildakiGunSayisi * YuzdeBoleni);
            }
            #endregion

            // Vergili faiz oranı hesabı
            decimal kkdfOrani = 0.00m;
            if (KKDF.HasValue)
            {
                kkdfOrani = KKDF.Value / YuzdeBoleni;
            }

            decimal bsmvOrani = 0.00m;
            if (BSMV.HasValue)
            {
                bsmvOrani = BSMV.Value / YuzdeBoleni;
            }
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

            // Ödemesiz sürede binecek faizi hesaplar
            for (int i = 1; i <= (faizOdemesizSure / anaParaOdemePeriyot); i++)
            {
                if (vadePeriyot == VadePeriyotEnum.Gun.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddDays(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Ay.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddMonths(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Yil.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddYears(anaParaOdemePeriyot.Value);

                var tempKalanBorc = kalanBorc;
                kalanBorc += tempKalanBorc * vergiliFaizOrani;
                kalanBorcVergisiz += tempKalanBorc * vergisizFaizOrani; // Faiz ödemesiz süre varsa ilk ödemede kullanılır
                birikenFaiz = kalanBorc - krediTutar.Value;
                birikenFaizVergisiz = kalanBorcVergisiz - krediTutar.Value; // Sadece faiz ödenen sürede kullanılır
            }

            // Sadece faiz ödenen süredeki veri girişleri
            for (int i = 1; i <= sadeceFaizOdenenSure; i++)
            {
                if (vadePeriyot == VadePeriyotEnum.Gun.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddDays(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Ay.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddMonths(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Yil.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddYears(anaParaOdemePeriyot.Value);

                if (i == 1 && birikenFaiz > 0)
                    kalanBorc -= birikenFaiz;

                odemePlani.Add(new OdemeSatiri
                {
                    Tarih = krediKullandirmaTarih.Value.ToString("dd.MM.yyyy"),
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
            if (kalanBorc > krediTutar)
            {
                if (vadePeriyot == VadePeriyotEnum.Gun.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddDays(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Ay.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddMonths(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Yil.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddYears(anaParaOdemePeriyot.Value);

                var taksitNo = 1;
                if (sadeceFaizOdenenSure.HasValue)
                {
                    sadeceFaizOdenenSure += 1;
                    taksitNo = sadeceFaizOdenenSure.Value;
                }

                odemePlani.Add(new OdemeSatiri
                {
                    Tarih = krediKullandirmaTarih.Value.ToString("dd.MM.yyyy"),
                    TaksitNo = (sadeceFaizOdenenSure.Value).ToString(),
                    TaksitMiktari = kalanBorc - krediTutar.Value,
                    Anapara = 0,
                    Faiz = kalanBorcVergisiz - krediTutar.Value,
                    KalanPara = krediTutar.Value
                });

                kalanBorc = krediTutar.Value;
                butunFaizOdendiMi = true;
            }

            // Kalan borç miktarına göre aylık ödenecek taksit tutarını hesaplar
            int odemePeriyotSayisi = ((vade.Value - anaParaOdemesizSure.Value) / anaParaOdemePeriyot.Value);
            if (butunFaizOdendiMi) // Fazladan ödeme yapıldıysa ödenecek tutarları buna göre hesaplar
            {
                odemePeriyotSayisi = ((vade.Value - anaParaOdemesizSure.Value) / anaParaOdemePeriyot.Value) -1;
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
                if (vadePeriyot == VadePeriyotEnum.Gun.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddDays(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Ay.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddMonths(anaParaOdemePeriyot.Value);
                else if (vadePeriyot == VadePeriyotEnum.Yil.ToDescription())
                    krediKullandirmaTarih = krediKullandirmaTarih.Value.AddYears(anaParaOdemePeriyot.Value);


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
                    Tarih = krediKullandirmaTarih.Value.ToString("dd.MM.yyyy"),
                    TaksitNo = (i + sadeceFaizOdenenSure.Value).ToString(),
                    TaksitMiktari = taksitBuAy,
                    Anapara = anaParaOdemesi,
                    Faiz = vergisizFaizTutari, 
                    KalanPara = kalanBorc < 0.01m ? 0 : kalanBorc
                });
            }

            var sb = new StringBuilder();
            var cultureInfo = new CultureInfo("tr-TR");
            if (ayrac == ".")
            {
                cultureInfo = new CultureInfo("en-GB");
            }
            
            foreach (var satir in odemePlani)
            {
                sb.AppendLine(string.Join("\t",
                    satir.Tarih, satir.TaksitNo,
                    satir.TaksitMiktari.ToString("N2", cultureInfo),
                    satir.Anapara.ToString("N2", cultureInfo),
                    satir.Faiz.ToString("N2", cultureInfo),
                    satir.KalanPara.ToString("N2", cultureInfo)
                ));
            }
            return sb.ToString();
        }
