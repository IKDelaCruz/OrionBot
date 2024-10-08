﻿using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[AllowAnonymous]
public class MessengerController : Controller
{
    private readonly string VerifyToken = "YourSecureVerifyToken"; // Your verify token that matches Facebook's
    private readonly string PageAccessToken = "EAALiYY9Syd8BO1bEIRiLZCARD65ykIUnShQX8gngJcjtbvw4cPJjam5xUxk3RCvfAD2BRWGE3toJZBHWC7pwwtyO8F4UKZBPcoZCQZCK4ol5pDFmlWJnLx2vji3soZBnb8kfYXDeCzIwkt8WIO8vjsjNyCureYxOF9JDzoJgqjl7B0FcenV26WhA9yZBgDid6ZBdQcHpIFMw5Rx4nEBo"; // Use your page access token

    [HttpGet]
    public ActionResult Hello()
    {
        return Content("Hello");
    }

    // GET: Webhook - This handles the verification from Facebook
    [HttpGet]
    public ActionResult Index(int id = 0)
    {
        string mode = Request.QueryString["hub.mode"];
        string token = Request.QueryString["hub.verify_token"];
        string challenge = Request.QueryString["hub.challenge"];

        if (mode == "subscribe" && token == VerifyToken)
        {
            return Content(challenge);
        }

        return new HttpStatusCodeResult(403); // Forbidden if token doesn't match
    }

    // POST: Webhook - This handles incoming messages and events from Facebook
    [HttpPost]
    public ActionResult Index()
    {
        string body;
        using (var reader = new StreamReader(Request.InputStream))
        {
            body = reader.ReadToEnd();
        }

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

                    if (messageText.ToLower() == "restart")
                    {
                        // Reset the user state and start the conversation again
                        ResetUserState(senderId);
                        StartConversation(senderId);
                    }
                    else if (messageEvent.message.quick_reply != null)
                    {
                        // Safely cast the payload to string and handle null values
                        string payload = messageEvent.message.quick_reply?.payload?.ToString();
                        if (!string.IsNullOrEmpty(payload))
                        {
                            HandleUserResponse(senderId, payload);
                        }
                    }
                    else if (messageEvent.message.text != null)
                    {
                        // Check if this is part of the registration process
                        if (IsAwaitingMobileNumber(senderId, messageText))
                        {
                            SaveMobileNumber(senderId, messageText);
                            AskForAddress(senderId);
                        }
                        else if (IsAwaitingAddress(senderId, messageText))
                        {
                            SaveAddress(senderId, messageText);
                            CompleteRegistration(senderId, GetSavedMobileNumber(senderId), messageText);
                        }
                        else
                        {
                            // If no context, start with the initial question
                            AskIfRegistered(senderId);
                        }
                    }
                }
            }
        }
    }

    // Handle Yes/No responses for registration
    private void HandleUserResponse(string senderId, string payload)
    {
        if (payload == "YES")
        {
            // User is registered, greet them
            //string userName = GetUserName(senderId); // Retrieve the user's name (mocked for now)
            var userProfile = GetUserProfile(senderId);
            GreetUserWithGenderAndLocation(senderId, userProfile);
        }
        else if (payload == "NO")
        {
            // User is not registered, ask for registration
            AskForMobileNumber(senderId);
        }
    }

    private void AskIfRegistered(string recipientId)
    {
        var messageData = new
        {
            recipient = new { id = recipientId },
            message = new
            {
                text = "Are you a registered user?",
                quick_replies = new[]
                {
                    new { content_type = "text", title = "Yes", payload = "YES" },
                    new { content_type = "text", title = "No", payload = "NO" }
                }
            }
        };

        SendMessageToUser(messageData);
    }

    private void GreetUserWithGenderAndLocation(string recipientId, dynamic userProfile)
    {
        string userName = userProfile.first_name;
        string gender = userProfile.gender == "male" ? "Mr." : "Ms.";
        string city = userProfile.location?.name;
        string locale = userProfile.locale;  // You can format the locale if necessary

        string greeting = $"Welcome back, {gender} {userName} from {city}! We're glad to have you.";

        var messageData = new
        {
            recipient = new { id = recipientId },
            message = new { text = greeting }
        };

        SendMessageToUser(messageData);
    }

    private void AskForMobileNumber(string recipientId)
    {
        var messageData = new
        {
            recipient = new { id = recipientId },
            message = new { text = "Please provide your mobile number to register." }
        };

        SendMessageToUser(messageData);
    }

    private void AskForAddress(string recipientId)
    {
        var messageData = new
        {
            recipient = new { id = recipientId },
            message = new { text = "Now, please provide your address." }
        };

        SendMessageToUser(messageData);
    }

    private void CompleteRegistration(string recipientId, string mobileNumber, string address)
    {
        // Save the user's information in your database (mocked here)
        SaveUserDetails(recipientId, mobileNumber, address);

        // First, send a message confirming registration
        var registrationMessage = new
        {
            recipient = new { id = recipientId },
            message = new { text = "Thank you for registering! Welcome to our platform." }
        };

        SendMessageToUser(registrationMessage);

        // Then, send a message with buttons to visit the website and YouTube
        var buttonMessage = new
        {
            recipient = new { id = recipientId },
            message = new
            {
                attachment = new
                {
                    type = "template",
                    payload = new
                    {
                        template_type = "button",
                        text = "You can also visit the following pages:",
                        buttons = new[]
                        {
                        new
                        {
                            type = "web_url",
                            url = "https://www.emigosolutions.com/",
                            title = "Visit Website"
                        },
                        new
                        {
                            type = "web_url",
                            url = "https://www.youtube.com/",
                            title = "Visit YouTube"
                        }
                    }
                    }
                }
            }
        };

        SendMessageToUser(buttonMessage);
    }


    private void SendMessageToUser(object messageData)
    {
        var client = new System.Net.WebClient();
        client.Headers.Add(System.Net.HttpRequestHeader.ContentType, "application/json");
        client.UploadString($"https://graph.facebook.com/v2.6/me/messages?access_token={PageAccessToken}", JsonConvert.SerializeObject(messageData));
    }

    // Restart conversation and reset user state

    private void ResetUserState(string recipientId)
    {
        // Example: Reset all user data or state in the database
        ClearUserDataFromDatabase(recipientId);
    }

    // Mock method for clearing user data (replace with actual database logic)
    private void ClearUserDataFromDatabase(string recipientId)
    {
        // Clear the user's registration or any ongoing state
        // Implement this based on how you're tracking user data
    }

    private void StartConversation(string recipientId)
    {
        var messageData = new
        {
            recipient = new { id = recipientId },
            message = new
            {
                text = "Hi! Let's start again. Are you a registered user?",
                quick_replies = new[]
                {
                    new { content_type = "text", title = "Yes", payload = "YES" },
                    new { content_type = "text", title = "No", payload = "NO" }
                }
            }
        };

        SendMessageToUser(messageData);
    }

    // Mock database and state management methods

    private bool IsAwaitingMobileNumber(string recipientId, string mobileNumber)
    {
        Regex mobileNumberRegex = new Regex(@"^(09\d{9}|(\+639)\d{9})$");

        // Placeholder logic to determine if we are waiting for the mobile number
        if (string.IsNullOrEmpty(mobileNumber))
        {
            return false;
        }

        // Check if the mobile number matches the pattern
        return mobileNumberRegex.IsMatch(mobileNumber);
    }

    private bool IsAwaitingAddress(string recipientId,string address)
    {
        //https://maps.googleapis.com/maps/api/js?key=AIzaSyBBePsuAk6MI1aS351hp5rGuyjYz0ABQfc
        // Placeholder logic to determine if we are waiting for the address
        string apiKey = "AIzaSyBBePsuAk6MI1aS351hp5rGuyjYz0ABQfc"; // Replace with your Google Maps API Key
        string apiUrl = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={apiKey}";

        using (var client = new WebClient())
        {
            try
            {
                // Send request to Google Maps API
                string response = client.DownloadString(apiUrl);
                JObject jsonResponse = JObject.Parse(response);

                // Check if the status is "OK" and if we have results
                if (jsonResponse["status"].ToString() == "OK" && jsonResponse["results"].HasValues)
                {
                    return true; // Address is valid
                }
            }
            catch (WebException ex)
            {
                // Handle any errors such as network issues or API quota exceeded
                System.Diagnostics.Debug.WriteLine("Error calling Google Maps API: " + ex.Message);
            }
        }

        return false; // Address is not valid or an error occurred
    }

    private void SaveMobileNumber(string recipientId, string mobileNumber)
    {
        // Mock logic to save the mobile number in the database
    }

    private void SaveAddress(string recipientId, string address)
    {
        // Mock logic to save the address in the database
    }

    private string GetSavedMobileNumber(string recipientId)
    {
        // Mock logic to retrieve the saved mobile number
        return "1234567890";
    }

    private void SaveUserDetails(string recipientId, string mobileNumber, string address)
    {
        // Mock logic to save user details in the database
    }

    private string GetUserName(string recipientId)
    {
        // Mock logic to fetch the user's name from the database
        return "John Doe";
    }
    // Fetch user profile information from Facebook
    private dynamic GetUserProfile(string userId)
    {
        string profileUrl = $"https://graph.facebook.com/{userId}?fields=first_name,gender,locale,location&access_token={PageAccessToken}";

        using (var client = new WebClient())
        {
            var result = client.DownloadString(profileUrl);
            return JsonConvert.DeserializeObject(result);
        }
    }
}
