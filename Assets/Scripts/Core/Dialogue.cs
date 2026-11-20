using System;

namespace Noctyra
{
    /// <summary>
    /// Botların ve oyuncunun gündüz tartışmasında kullandığı replikler.
    /// İsimlere ek getirmemek için cümleler "hakkında / üzerinde / için" gibi
    /// ek gerektirmeyen kalıplarla kurulmuştur.
    /// </summary>
    public static class Dialogue
    {
        public static readonly string[] Names =
        {
            "Ayşe", "Mehmet", "Zeynep", "Ali", "Elif", "Can", "Deniz", "Burak",
            "Selin", "Kerem", "Defne", "Emre", "Ece", "Murat", "Yasemin", "Barış",
            "Naz", "Onur", "Pelin", "Tuna", "Derya", "Koray"
        };

        static string Pick(Random rng, string[] arr)
        {
            return arr[rng.Next(arr.Length)];
        }

        static readonly string[] accuse =
        {
            "{0} hakkında kötü bir hissim var.",
            "Gözüm {0} üzerinde, bir şeyler saklıyor gibi.",
            "Bence vampir {0}. Oyum onda.",
            "{0}, bu kadar sessiz kalman şüpheli.",
            "Şüphelendiğim kişi: {0}.",
            "{0} dün geceden beri tuhaf davranıyor."
        };

        static readonly string[] humanAccuse =
        {
            "Bence vampir {0}!",
            "{0} hakkında ciddi şüphelerim var.",
            "Herkes {0} üzerine dikkat kesilsin.",
            "{0}, kendini açıklaman gerek."
        };

        static readonly string[] defend =
        {
            "Ben masumum! Vampirler beni susturmak istiyor.",
            "Bana yüklenmeyin, ben sadece bir köylüyüm.",
            "Yanlış kişiyi suçluyorsunuz, buna pişman olacaksınız.",
            "Delil nerede? Sadece suçlama yapıyorsunuz.",
            "Vampir olsam bu kadar ortada durur muydum?"
        };

        static readonly string[] humanDefend =
        {
            "Ben vampir değilim, bana güvenin.",
            "Beni asarsanız vampirlerin işine yarar.",
            "Sakin olun, bana bakmak yerine gerçek şüphelilere bakın."
        };

        static readonly string[] agree =
        {
            "Ben de aynı şeyi düşünüyorum, mantıklı.",
            "Katılıyorum, ben de dikkat ettim.",
            "Evet, bu bana da garip gelmişti.",
            "Haklısın, üzerinde durmaya değer."
        };

        static readonly string[] counter =
        {
            "Acele etmeyelim, ortada kanıt yok.",
            "Bu suçlama bana haksızlık gibi geldi.",
            "Bence yanlış yoldasınız.",
            "Panikle karar verirsek vampirler kazanır."
        };

        static readonly string[] neutral =
        {
            "Henüz net bir şey göremiyorum, dikkatli olalım.",
            "Herkes sakin olsun, hata yaparsak vampirler kazanır.",
            "Dün geceyi düşünüyorum... Kim uyanıktı acaba?",
            "Sessiz kalanları izleyin.",
            "Kimin kime oy verdiğini aklınızda tutun."
        };

        static readonly string[] seerVamp =
        {
            "Ben falcıyım! Dün gece baktım, {0} bir vampir!",
            "Falcı benim. {0} gerçek yüzünü gösterdi: vampir!"
        };

        static readonly string[] seerClean =
        {
            "Ben falcıyım. {0} temiz çıktı, güvenebilirsiniz.",
            "Falcı olarak söylüyorum: {0} vampir değil."
        };

        static readonly string[] doctorSaved =
        {
            "Ben doktorum. Dün gece {0} için nöbet tuttum, vampirler başaramadı.",
            "Doktor benim. {0} korumamdaydı, saldırı boşa gitti."
        };

        static readonly string[] doctorGeneric =
        {
            "Ben doktorum. Her gece birini korumaya çalışıyorum.",
            "Köyün doktoruyum, gözüm hep hastalarımda."
        };

        static readonly string[] counterClaim =
        {
            "{0} yalan söylüyor, o bir vampir!",
            "{0} rolünü uyduruyor, sakın inanmayın.",
            "Hiç inandırıcı değil, {0} blöf yapıyor."
        };

        public static string Accuse(Random rng, string target) { return string.Format(Pick(rng, accuse), target); }
        public static string HumanAccuse(Random rng, string target) { return string.Format(Pick(rng, humanAccuse), target); }
        public static string Defend(Random rng) { return Pick(rng, defend); }
        public static string HumanDefend(Random rng) { return Pick(rng, humanDefend); }
        public static string Agree(Random rng) { return Pick(rng, agree); }
        public static string Counter(Random rng) { return Pick(rng, counter); }
        public static string Neutral(Random rng) { return Pick(rng, neutral); }
        public static string SeerVamp(Random rng, string t) { return string.Format(Pick(rng, seerVamp), t); }
        public static string SeerClean(Random rng, string t) { return string.Format(Pick(rng, seerClean), t); }
        public static string DoctorSaved(Random rng, string t) { return string.Format(Pick(rng, doctorSaved), t); }
        public static string DoctorGeneric(Random rng) { return Pick(rng, doctorGeneric); }
        public static string CounterClaim(Random rng, string t) { return string.Format(Pick(rng, counterClaim), t); }
    }
}
