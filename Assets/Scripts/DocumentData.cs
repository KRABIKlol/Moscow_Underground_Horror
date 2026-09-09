using UnityEngine;

/// Удостоверение посетителя. Все нарушения видны прямо в полях - игрок может решить сам.
[System.Serializable]
public class DocumentData
{
    public string fullName;
    public string birthDate;     // дд.мм.гггг
    public string documentId;    // МУ-XXXXXX
    public string expiryDate;    // дд.мм.гггг
    public string purpose;       // цель визита

    public bool valid;
    [Tooltip("Human readable defect. Empty when the document is clean.")]
    public string problem;
}

public static class DocumentGenerator
{
    // Игровая "сегодняшняя" дата. Игрок сверяет с ней срок действия.
    public static int Day = 14, Month = 11, Year = 2026;
    public static string Today => $"{Day:00}.{Month:00}.{Year}";

    static readonly string[] Male    = { "Андрей", "Сергей", "Дмитрий", "Николай", "Виктор", "Олег", "Павел", "Игорь" };
    static readonly string[] Female  = { "Ольга", "Ирина", "Марина", "Татьяна", "Елена", "Наталья", "Светлана" };
    static readonly string[] Surname = { "Соколов", "Титов", "Ерохин", "Гаврилов", "Панкратов", "Лебедев", "Зимин", "Носов" };

    static readonly string[] Purposes = {
        "Плановое обслуживание", "Доставка", "Совещание", "Подрядные работы",
        "Инспекция", "Собеседование", "Вывоз оборудования"
    };

    public enum Defect { None, Expired, Underage, BadNumber, NoPurpose }

    public static DocumentData Create(float badChance)
    {
        var d = new DocumentData();

        bool female = Random.value < 0.45f;
        string sur = Surname[Random.Range(0, Surname.Length)];
        d.fullName = female
            ? sur + "а " + Female[Random.Range(0, Female.Length)]
            : sur + " " + Male[Random.Range(0, Male.Length)];

        Defect defect = Defect.None;
        if (Random.value < badChance)
            defect = (Defect)Random.Range(1, 5);

        // по умолчанию всё чистое
        d.birthDate   = Date(Random.Range(1960, Year - 25));
        d.documentId  = "МУ-" + Random.Range(100000, 999999);
        d.expiryDate  = Date(Random.Range(Year + 1, Year + 5));
        d.purpose     = Purposes[Random.Range(0, Purposes.Length)];

        switch (defect)
        {
            case Defect.Expired:
                d.expiryDate = Date(Random.Range(Year - 6, Year));
                d.problem = "Срок действия истёк";
                break;

            case Defect.Underage:
                d.birthDate = Date(Random.Range(Year - 17, Year - 12));
                d.problem = "Возраст меньше 18 лет";
                break;

            case Defect.BadNumber:
                d.documentId = Random.value < 0.5f
                    ? "МУ-" + Random.Range(100, 9999)                  // слишком короткий
                    : "XX-" + Random.Range(100000, 999999);            // не тот префикс
                d.problem = "Номер не соответствует формату МУ-XXXXXX";
                break;

            case Defect.NoPurpose:
                d.purpose = "—";
                d.problem = "Не указана цель визита";
                break;

            default:
                d.problem = "";
                break;
        }

        d.valid = defect == Defect.None;
        return d;
    }

    static string Date(int year) => $"{Random.Range(1, 29):00}.{Random.Range(1, 13):00}.{year}";
}
