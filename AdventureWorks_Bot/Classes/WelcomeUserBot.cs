using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdventureWorks_Bot.Helpers;
using AdventureWorks_Bot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace AdventureWorks_Bot.Classes
{
    public class WelcomeUserBot : IBot
    {
        private const string WelcomeMessage = "Welcome to Adventure Works";

        private const string InfoMessage = "How can we help you? You can talk to our assistant. Try saying 'I want to access' or 'show me the products list'. If you want to know about a specific product, you can use 'please tell me about mountain bike' or similar messages. Our smart digital assistant will do its best to help you!";

        private const string PatternMessage = @"You can also say help to display some options";

        private readonly BotService _services;
        public static readonly string LuisKey = "AdventureWorks_BotBot";

        public WelcomeUserBot(BotService services)
        {
            _services = services ?? throw new System.ArgumentNullException(nameof(services));

            if (!_services.LuisServices.ContainsKey(LuisKey))
                throw new System.ArgumentException($"Invalid configuration....");
        }

        static bool welcome = false;

        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = new CancellationToken())
        {
            // aqui se procesan las cards
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                // luis
                var recognizer = await _services.LuisServices[LuisKey].RecognizeAsync(turnContext, cancellationToken);
                var topIntent = recognizer?.GetTopScoringIntent();

                if (topIntent != null && topIntent.HasValue && topIntent.Value.intent != "None")
                {
                    switch (topIntent.Value.intent)
                    {
                        case Constants.LoginIntent:
                            var ent = LuisParser.GetEntityValue(recognizer);
                            var t_v = ent.Split("_", 2);

                            switch (t_v[0])
                            {
                                case Constants.EmailLabel:
                                    var customer = await WebApiService.GetCustomer(t_v[1]);
                                    var userName = "";

                                    if (customer != null)
                                    {
                                        userName = customer.CustomerName;

                                        var hero = new HeroCard();
                                        hero.Title = "Welcome";
                                        hero.Text = customer.CustomerName;
                                        hero.Subtitle = customer.CompanyName;

                                        var response = turnContext.Activity.CreateReply();
                                        response.Attachments = new List<Attachment>() { hero.ToAttachment() };
                                        await turnContext.SendActivityAsync(response, cancellationToken);

                                        //await turnContext.SendActivityAsync($"Welcome {userName}");
                                    }
                                    else
                                        await turnContext.SendActivityAsync($"User not found. Pleae try again");
                                    break;
                                default:
                                    await turnContext.SendActivityAsync("Please add your email to your login message");
                                    break;
                            }
                            break;
                        case Constants.ProductInfoIntent:
                            var entity = LuisParser.GetEntityValue(recognizer);

                            var type_value = entity.Split("_", 2);

                            switch (type_value[0])
                            {
                                case Constants.ProductLabel:
                                case Constants.ProductNameLabel:
                                    var product = "_";
                                    var message = "Our Top 5 Products are:";

                                    if (type_value[0] == Constants.ProductNameLabel)
                                    {
                                        product = type_value[1];
                                        message = "Your query returned the following products: ";
                                    }

                                    var products = await WebApiService.GetProducts(product);
                                    var data = "No results";

                                    var typing = Activity.CreateTypingActivity();
                                    var delay = new Activity { Type = "delay", Value = 5000 };

                                    if (products != null)
                                    {
                                        var responseProducts = turnContext.Activity.CreateReply();
                                        responseProducts.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                                        responseProducts.Attachments = new List<Attachment>();

                                        foreach (var item in products)
                                        {
                                            var card = new HeroCard();
                                            card.Subtitle = item.ListPrice.ToString("N2");
                                            card.Title = item.Name;
                                            card.Text = $"{item.Category} - {item.Model} - {item.Color}";

                                            card.Images = new List<CardImage>()
                                                {
                                                    new CardImage()
                                                    {

                                                        Url = $"data:image/gif;base64,{item.Photo}"
                                                    }
                                                };

                                            var plAttachment = card.ToAttachment();
                                            responseProducts.Attachments.Add(plAttachment);
                                        }

                                        var activities = new IActivity[]
                                        {
                                            typing,
                                            delay,
                                            MessageFactory.Text($"{message}: "),
                                            responseProducts,
                                            MessageFactory.Text("What else can I do for you?")
                                        };

                                        await turnContext.SendActivitiesAsync(activities);

                                    }
                                    else
                                    {
                                        var activities = new IActivity[]
                                        {   typing,
                                            delay,
                                            MessageFactory.Text($"{message}: {data}"),
                                            MessageFactory.Text("What else can I do for you?")
                                        };

                                        await turnContext.SendActivitiesAsync(activities);
                                    }


                                    break;
                                default:
                                    break;
                            }
                            break;
                        default:
                            break;
                    }



                }
                else
                {
                    var text = turnContext.Activity.Text.ToLowerInvariant();
                    switch (text)
                    {
                        case "help":
                            await SendIntroCardAsync(turnContext, cancellationToken);
                            break;
                        default:
                            await turnContext.SendActivityAsync("I did not understand you, sorry. Try again with a different sentence, please", cancellationToken: cancellationToken);
                            break;
                    }
                }
            }
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (!welcome)
                {
                    welcome = true;

                    await turnContext.SendActivityAsync($"Hi there. {WelcomeMessage}", cancellationToken: cancellationToken);
                    await turnContext.SendActivityAsync(InfoMessage, cancellationToken: cancellationToken);
                    await turnContext.SendActivityAsync(PatternMessage, cancellationToken: cancellationToken);
                }
            }
            else
            {
                await turnContext.SendActivityAsync($"{turnContext.Activity.Type} activity detected");
            }
        }

        private static async Task SendIntroCardAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var response = turnContext.Activity.CreateReply();

            var card = new HeroCard();
            card.Title = WelcomeMessage;
            card.Text = InfoMessage;
            card.Images = new List<CardImage>() { new CardImage("https://drive.google.com/uc?id=1eE_WlkW8G9cSI_w9heIWeo53ZkMtQu4x") };
            card.Buttons = new List<CardAction>()
            {
                new CardAction(ActionTypes.OpenUrl, "Enter my credentials", null, "Enter my credentials", "Enter my credentials", "Login"),
                new CardAction(ActionTypes.OpenUrl, "Show me the product list", null, "Show me the product list", "Show me the product list", "ProductInfo"),
            };

            response.Attachments = new List<Attachment>() { card.ToAttachment() };
            await turnContext.SendActivityAsync(response, cancellationToken);
        }
    }
}
