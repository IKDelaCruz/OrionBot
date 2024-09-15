using Newtonsoft.Json;
using System.IO;
using System.Net;
using System.Web.Mvc;

public class MessengerWebhookController : Controller
{
    private readonly string VerifyToken = "EAALiYY9Syd8BO1S0oVnfQmBD9jjbkdrUHo6vp0ZBYGoGNEeZBhHWQlKWKKLXglzdfbBwFQXIgBr9r6anxTF0mmuz05u7tp3Q9iv9MmVkzslSPCcnTu2iZAwibFPngdt0m7yJ9RpfwWgbCZBUV8tRAQvoLnRe9JIKGiFYN0unoUVjLKIThYCfnaB08ZCgavIzEC0EfxZCIOx8OSpZB0I";

    // GET: Webhook (for verification)
    [HttpGet]
    public ActionResult Index(string hub_mode, string hub_challenge, string hub_verify_token)
    {
        if (hub_mode == "subscribe" && hub_verify_token == VerifyToken)
        {
            return Content(hub_challenge);
        }
        return new HttpStatusCodeResult(403); // Forbidden
    }

    // POST: Webhook (to handle messages)
    [HttpPost]
    public ActionResult Index()
    {
        string body;
        using (var reader = new StreamReader(Request.InputStream))
        {
            body = reader.ReadToEnd();
        }

        // Parse the incoming message
        dynamic messageData = JsonConvert.DeserializeObject(body);
        ProcessMessage(messageData);

        return new HttpStatusCodeResult(200);
    }

    private void ProcessMessage(dynamic messageData)
    {
        foreach (var entry in messageData.entry)
        {
            foreach (var messageEvent in entry.messaging)
            {
                var senderId = messageEvent.sender.id.ToString();
                if (messageEvent.message != null)
                {
                    string messageText = messageEvent.message.text;
                    // Handle the received message
                    SendMessage(senderId, "You said: " + messageText);
                }
            }
        }
    }

    private void SendMessage(string recipientId, string messageText)
    {
        string accessToken = "YOUR_PAGE_ACCESS_TOKEN";
        var messageData = new
        {
            recipient = new { id = recipientId },
            message = new { text = messageText }
        };

        var client = new WebClient();
        client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
        client.UploadString($"https://graph.facebook.com/v2.6/me/messages?access_token={accessToken}", JsonConvert.SerializeObject(messageData));
    }
}