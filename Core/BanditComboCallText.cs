using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The words of the combo calls. PLACEHOLDERS: English and German from the list, the other eight languages unreviewed
// drafts. They show only with bandits.combo.calls on, which stays off until the final texts stand here. The game's speech bubble reads
// TEXTKEY_SPEECH_ plus its id; Core takes ids the game does not use (its values run 0 to 96).
internal static class BanditComboCallText
{
    private const string SpeechKeyPrefix = "TEXTKEY_SPEECH_";
    private const int FirstSpeechId = 9310;
    // Each kind keeps its block of ids.
    private const int IdsPerKind = 10;

    private static readonly Dictionary<BanditCallKind, UniqueCandidateTranslations[]> Sentences = new()
    {
        [BanditCallKind.Hold] = new UniqueCandidateTranslations[]
        {
            new("Now, I've got him!", "지금이다, 잡았다!", "Maintenant, je le tiens !", "Jetzt, ich hab ihn!", "Сейчас, он у меня!", "现在，我抓住他了！", "現在，我抓住他了！", "今だ、捕まえたぞ！", "¡Ahora, lo tengo!", "Agora, peguei ele!"),
            new("Hold still!", "가만히 있어!", "Bouge pas !", "Halt still!", "Стой смирно!", "别动！", "別動！", "動くな！", "¡Quieto!", "Fica parado!"),
            new("He's stuck, hit him!", "꼼짝 못 한다, 쳐라!", "Il est coincé, frappe !", "Er steckt fest, drauf!", "Он застрял, бей!", "他动不了，打！", "他動不了，打！", "動けないぞ、やれ！", "¡Está atrapado, dale!", "Ele está preso, bate!"),
        },
        [BanditCallKind.Weaken] = new UniqueCandidateTranslations[]
        {
            new("He's open, now!", "빈틈이다, 지금!", "Il est à découvert, vas y !", "Er ist offen, jetzt!", "Он открыт, давай!", "他露出破绽了，上！", "他露出破綻了，上！", "隙だらけだ、今だ！", "¡Está expuesto, ahora!", "Ele está aberto, agora!"),
            new("Weak spot, go!", "약점이다, 가!", "Point faible, allez !", "Schwachstelle, los!", "Слабое место, вперёд!", "弱点，上！", "弱點，上！", "弱点だ、行け！", "¡Punto débil, vamos!", "Ponto fraco, vai!"),
            new("Take him down now!", "끝장내라!", "Achève le !", "Mach ihn fertig!", "Прикончи его!", "解决他！", "解決他！", "とどめだ！", "¡Acaba con él!", "Acaba com ele!"),
        },
        [BanditCallKind.Bunch] = new UniqueCandidateTranslations[]
        {
            new("They're bunched up, fire!", "뭉쳐 있다, 쏴라!", "Ils sont groupés, feu !", "Die stehen eng, Feuer!", "Они сбились в кучу, огонь!", "他们挤在一起，开火！", "他們擠在一起，開火！", "固まってるぞ、撃て！", "¡Están amontonados, fuego!", "Estão amontoados, fogo!"),
            new("All in one spot!", "전부 한 곳에 있다!", "Tous au même endroit !", "Alle auf einem Fleck!", "Все в одном месте!", "全在一个地方！", "全在一個地方！", "全員一か所だ！", "¡Todos en un solo sitio!", "Todos num lugar só!"),
            new("Light them up!", "태워 버려!", "Faites les brûler !", "Brennt sie nieder!", "Сожгите их!", "烧了他们！", "燒了他們！", "焼き払え！", "¡Quemadlos!", "Queimem eles!"),
        },
    };

    internal static int CountOf(BanditCallKind kind) => Sentences.TryGetValue(kind, out var list) ? list.Length : 0;

    internal static int SpeechId(BanditCallKind kind, int index) => FirstSpeechId + (int)kind * IdsPerKind + index;

    internal static string SpeechKey(int speechId) => SpeechKeyPrefix + speechId.ToString(CultureInfo.InvariantCulture);

    // A sentence at random, never the one the kind showed last; last is -1 before the first.
    internal static int Pick(int count, int last, double roll)
    {
        if (count <= 1) return 0;
        var choices = last >= 0 && last < count ? count - 1 : count;
        var index = Math.Min((int)(roll * choices), choices - 1);
        return last >= 0 && last < count && index >= last ? index + 1 : index;
    }

    internal static IEnumerable<(string Key, UniqueCandidateTranslations Text)> TextRows()
    {
        foreach (var pair in Sentences)
            for (var i = 0; i < pair.Value.Length; i++)
                yield return (SpeechKey(SpeechId(pair.Key, i)), pair.Value[i]);
    }
}
