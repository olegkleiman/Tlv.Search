using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OpenAIDrive
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding();

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);
            IConfiguration config = builder.Build();

            var connectionString = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");

            var openaiKey = config["OPENAI_KEY"];
            if( string.IsNullOrEmpty(openaiKey) )
            {
                Console.WriteLine("OpenAI key not found in configuration");
                return;
            }

            var openaiAzureKey = config["OPENAI_AZURE_KEY"];

            var openaiEndpoint = config["OPENAI_ENDPOINT"];
            if( string.IsNullOrEmpty(openaiEndpoint) )
            {
                Console.WriteLine("OpenAI endpoint not found in configuration");
                return;
            }

            try
            {
                var skKernelBuilder = Kernel.CreateBuilder();
                skKernelBuilder.AddAzureOpenAIChatCompletion(
                                                     "gpt35turbo", // Azure OpenAI Deployment Name
                                                     openaiEndpoint,
                                                     openaiAzureKey
                                                     );
                var kernel = skKernelBuilder.Build();


                // Invoke the kernel with a chat prompt and display the result
                string chatPrompt = @"
                    <message role=""user"">What is Seattle?</message>
                    <message role=""system"">Respond with JSON.</message>
                ";
                Console.WriteLine(await kernel.InvokePromptAsync(chatPrompt));

                List<string> examplePrompts = new()
                {
                    //"איפה נמצאת עיריית תל-אביב"
                    " What is the best football club in Brazil?"
                };

                // Azure OpenAI package
                var client = new OpenAIClient(openaiKey, new OpenAIClientOptions());

                //
                // Embeddings
                //
                List<float> queryVector = new();
                EmbeddingsOptions eo = new(
                    deploymentName: "text-embedding-ada-002",
                    input: examplePrompts);

                Response<Embeddings> _response = await client.GetEmbeddingsAsync(eo);
                foreach (var item in _response.Value.Data)
                {
                    var embedding = item.Embedding;

                    for (int i = 0; i < embedding.Length; i++)
                    {
                        float value = embedding.Span[i];
                        queryVector.Add(value);
                    }
                }

                //
                // Completions
                //
                foreach (string prompt in examplePrompts)
                {
                    CultureInfo ci = new("he-IL");
                    Console.WriteLine($"Input: {prompt}", ci.Name);

                    var context = "\r\nפרטי זכאות\r\n\r\nהתוקף החוקי\r\nתקנות הסדרים במשק המדינה (הנחה מארנונה), התשנ\"ג - 1993.\r\n\r\nפרטי הזכאות\r\nבעלי הכנסה נמוכה.\r\n\r\nהגדרת הכנסה\r\n\r\n1. הכנסה חודשית ברוטו של המחזיק בנכס והמתגוררים איתו, לרבות ילד במשפחת אומנה, לחודשים אוקטובר-נובמבר-דצמבר לשנת הכספים הקודמת לבקשה או לחודשים ינואר עד דצמבר לשנת הכספים הקודמת לבקשה, לפי בחירתו.\r\n\r\n• אם בן או בת של המחזיק בנכס מתגוררים איתו תילקח בחשבון מחצית מהכנסתם החודשית, לעניין זה לא תובא בחשבון לגבי בן אחד או בת אחת בלבד, הכנסה חודשית עד גובה שכר המינימום, ואם אותה הכנסה חודשית עולה על גובה שכר המינימום, לא יובא בחשבון החלק מההכנסה החודשית השווה לגובה שכר המינימום: בפסקת משנה זאת, \"שכר מינימום\" - שכר המינימום כהגדרתו בחוק שכר מינימום, התשמ\"ז - 1987, בשיעורו המעודכן ל-1 בינואר של שנת הכספים שבעדה מבוקשת ההנחה.\r\n\r\n• ב-1 בינואר 2023 שכר המינימום - 5,300 ₪.\r\n\r\n \r\n\r\n2. בחישוב ההכנסה תילקח בחשבון הכנסה מכל מקור שהוא לרבות משכורת, גמלה (פנסיה ממקום העבודה), שכר דירה*, מלגות, תמיכת צה\"ל, קצבת תשלומים מחו\"ל, פיצויים, תמיכות אחרות ותשלומים שמשלם המוסד לביטוח לאומי למעט קצבת ילדים, מענק לימודים ל\"הורה עצמאי\" לפי חוק סיוע למשפחות שבראשן הורה עצמאי, התשנ\"ב-1992, קצבת זקנה, קצבת שאירים וגמלה לפי תקנות הביטוח הלאומי (ילד נכה), התש\"ע - 2010.\r\n\r\n* הכנסות מדמי שכירות - רק בסכום העולה על דמי השכירות שמשלם המחזיק בעבור דירה אחרת ששכר למגוריו.\r\n\r\nשיעור ההנחה\r\n\r\n20%-90% מכל שטח הדירה.\r\nמחושב לפי סכום ההכנסה ומספר הנפשות, כמפורט בטבלה.\r\n\r\n\r\nמסמכים נדרשים\r\n\r\nחובה לצרף לכל בקשה את כל המסמכים (בעבור המבקש או המבקשת ובעבור כל מי שמתגוררים בנכס מעל גיל 18 ויש ביניהם קרבה משפחתית).\r\n\r\nהכנסה חודשית ממוצעת תחושב לבחירתך לפי חודשים אוקטובר- נובמבר- דצמבר לשנת הכספים הקודמת לבקשה או לחודשים ינואר עד דצמבר לשנת הכספים הקודמת לבקשה. בהתאם לבחירתך יש לצרף את כל המסמכים הנדרשים: \r\n\r\n    טופס \"בקשה לקבלת הנחה בארנונה על פי הכנסה חודשית ממוצעת ולפי מספר נפשות בדירה\".\r\n    צילום תעודת זהות שבה מופיעה כתובת עדכנית של המבקש או המבקשת, בתחומי העיר תל־אביב-יפו, התואמת את הכתובת שבחשבון הארנונה על שמם.\r\n    אם מבקש או מבקשת ההנחה רשומים במרשם האוכלוסין ברשות אחרת - אישור מהרשות האחרת שאינם מקבלים הנחה בתחומה.\r\n    מהמוסד לביטוח לאומי:\r\n    * \"אישור ביטוח ושכר\" מביטוח לאומי (דוח מעסיקים).\r\n    * אישור על זכאות לגמלאות (הכולל את כל התשלומים מביטוח לאומי). אם קיימת זכאות לגמלה - יש לצרף גם אישור המציין את סכום הגמלה.\r\n    מסמכים המעידים על הכנסות נוספות (למעט שכר) מכל מקור שהוא*\r\n    יודגש כי לא מן הנמנע, כי יידרשו מסמכים נוספים.\r\n\r\n* לרבות תשלומים שהמוסד לביטוח לאומי משלם (למעט אלה שהוחרגו במפורש) ולרבות מענק זקנה, פנסיה ממקום העבודה, קצבת שאירים, קצבת נכות, שכר דירה, תמיכת צה\"ל, קצבת תשלומים מחו\"ל, פיצויים, הבטחת הכנסה, השלמת הכנסה, תמיכות ואחר (הסבר לגבי ההכנסות).\r\n\r\nכמו כן, צריך לצרף בהתאם להגדרתך מטה:\r\n\r\n    שכירים:  תלושי משכורת\r\n\r\n    עצמאים או עצמאים שהם גם שכירים:  \r\n        דוח שומה אחרון שברשותך.\r\n        אישור ממס הכנסה שדוח זה הוא הדוח האחרון (רק אם הדוח אינו מהשנה הקודמת לבקשה).\r\n\r\nבפני מבקש ההנחה עומדות שתי אפשרויות לבחירה: לחתום על טופס ויתור סודיות​​ המאפשר לעיריית תל אביב-יפו לקבל מהמוסד לביטוח לאומי את המסמכים הרלוונטיים לצורך בדיקת זכאות להנחה או להמציא אישורים מהמוסד לביטוח לאומי - אישור זכאות לגמלאות, אישור ביטוח ושכר (דוח מעסיקים) ומעמד (ככל שנדרש).\r\n\r\nהנחיות נוספות\r\n\r\nזוגות נשואים או הרשומים במאגר ידועים וידועות בציבור של עיריית תל אביב-יפו - ההנחה תינתן על כל שטח הנכס אלא אם בסוג ההנחה המבוקשת יש הגבלה על שטח הנכס עליו תוענק ההנחה.\r\n\r\nאם עוד לא נרשמת למאגר, אפשר לעשות זאת בקישור ולאחר קבלת האישור להגיש את הבקשה. רישום למאגר ידועים וידועות בציבור.\r\n\r\nשותף או שותפה בנכס (לא כולל זוגות נשואים או ידועים וידועות בציבור הרשומים במאגר העירייה) - ההנחה תינתן לבעלי הזכאות, על פי החלק היחסי בשטח הנכס, בהתאם למספר המחזיקים הרשומים בנכס.​​​​";
                        
                        //string userMessage = $"{context}. Answer in Hebrew the following question from the text above. Q: {prompt} A:";
                    string userMessage = "סכם בבקשה את הטקס הבא \n: ";

                    ChatCompletionsOptions cco = new()
                    {
                        DeploymentName = "gpt-3.5-turbo-1106", //,"gpt-4"
                        Messages =
                        {
                            new ChatRequestSystemMessage(@"You are a help assistant that summarized the user input."),
                            new ChatRequestUserMessage(context)
                        },
                        Temperature = (float)0.7,
                        MaxTokens = 800,
                        NucleusSamplingFactor = (float)0.95,
                        FrequencyPenalty = 0,
                        PresencePenalty = 0,
                    };

                    Response<ChatCompletions> responseWithoutStream = await client.GetChatCompletionsAsync(cco);
                    ChatResponseMessage responseMessage = responseWithoutStream.Value.Choices[0].Message;
                    Console.WriteLine($"[{responseMessage.Role}]: {responseMessage.Content}");

                    //ChatCompletions _response = responseWithoutStream.Value;

                    //await foreach(StreamingChatCompletionsUpdate chatUpdate in client.GetChatCompletionsStreaming(cco) )
                    //{
                    //    if( chatUpdate.Role.HasValue )
                    //        Console.WriteLine($"{chatUpdate.Role.Value.ToString().ToUpperInvariant}:");

                    //    if( !string.IsNullOrEmpty(chatUpdate.ContentUpdate) )
                    //        Console.WriteLine(chatUpdate.ContentUpdate);
                    //};

                    // Legacy completions
                    CompletionsOptions ops = new()
                    {
                        MaxTokens = 800,
                        FrequencyPenalty = 0,
                        PresencePenalty = 0,
                        Temperature = 0.7f,
                        NucleusSamplingFactor = (float)0.95,
                        DeploymentName = "text-davinci-003" //"gpt-3.5-turbo-instruct" // 
                    };
                    ops.Prompts.Add(userMessage + context);

                    Response<Completions> response = client.GetCompletions(ops);
                    string completion = response.Value.Choices[0].Text;
                    Console.WriteLine(completion);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
