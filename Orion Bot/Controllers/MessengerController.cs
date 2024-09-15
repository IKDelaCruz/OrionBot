using System;
using System.IO;
using System.Web.Mvc;
using Newtonsoft.Json;

[AllowAnonymous]
public class MessengerController : Controller
{
    private readonly string VerifyToken = "YourSecureVerifyToken"; // Your verify token that matches Facebook's

    [HttpGet]
    public ActionResult Hello()
    {
        return Content("Hello");
    }
    // GET: Webhook - This handles the verification from Facebook
    [HttpGet]
    public ActionResult Index(int id = 0)
    {
        // Access query string parameters directly using Request.QueryString
        string mode = Request.QueryString["hub.mode"];
        string token = Request.QueryString["hub.verify_token"];
        string challenge = Request.QueryString["hub.challenge"];

        // Validate the verify token
        if (mode == "subscribe" && token == VerifyToken)
        {
            // If token matches, return the challenge token to confirm webhook subscription
            return Content(challenge);
        }

        // If verification fails, return 403 Forbidden
        return new HttpStatusCodeResult(403);
    }

    // POST: Webhook - This handles incoming messages and events from Facebook
    [HttpPost]
    public ActionResult Index()
    {
        // Read the body of the incoming request from Facebook
        string body;
        using (var reader = new StreamReader(Request.InputStream))
        {
            body = reader.ReadToEnd();
        }

        // Parse the incoming message using Newtonsoft.Json
        dynamic messageData = JsonConvert.DeserializeObject(body);

        // Process the message
        ProcessMessage(messageData);

        // Return 200 OK to acknowledge that the request was received successfully
        return new HttpStatusCodeResult(200);
    }

    // A helper method to process the incoming message
    private void ProcessMessage(dynamic messageData)
    {
        // Iterate over the entries (Facebook sends events in batches)
        foreach (var entry in messageData.entry)
        {
            // Iterate over the messages
            foreach (var messageEvent in entry.messaging)
            {
                var senderId = messageEvent.sender.id.ToString();

                // Check if the event contains a message
                if (messageEvent.message != null)
                {
                    string messageText = messageEvent.message.text;

                    // Respond to the user with a basic text message
                    SendMessage(senderId, "You said: " + messageText);
                }
            }
        }
    }

    // A helper method to send a response message back to the user via Facebook
    private void SendMessage(string recipientId, string messageText)
    {
        string accessToken = "EAALiYY9Syd8BO1S0oVnfQmBD9jjbkdrUHo6vp0ZBYGoGNEeZBhHWQlKWKKLXglzdfbBwFQXIgBr9r6anxTF0mmuz05u7tp3Q9iv9MmVkzslSPCcnTu2iZAwibFPngdt0m7yJ9RpfwWgbCZBUV8tRAQvoLnRe9JIKGiFYN0unoUVjLKIThYCfnaB08ZCgavIzEC0EfxZCIOx8OSpZB0I"; // Use your page access token
        var messageData = new
        {
            recipient = new { id = recipientId },
            message = new { text = messageText }
        };

        var client = new System.Net.WebClient();
        client.Headers.Add(System.Net.HttpRequestHeader.ContentType, "application/json");

        // Send the message using Facebook's Send API
        client.UploadString($"https://graph.facebook.com/v2.6/me/messages?access_token={accessToken}", JsonConvert.SerializeObject(messageData));
    }
}
