
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Vision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Cognitive.LUIS;

namespace Bottest1
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            if (activity.Type == ActivityTypes.Message)
            {
                
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
				Activity reply = activity.CreateReply();
                //Trace.TraceInformation(JsonConvert.SerializeObject(activity, Formatting.Indented));

                if (activity.Attachments?.Count > 0 && activity.Attachments.First().ContentType.StartsWith("image"))//IF NULL 不會往下,有東西才繼續run
                {
                    //user傳一張照片
                    ImageTemplate(reply, activity.Attachments.First().ContentUrl);
                    
                }
                //quick menu
                else if(activity.Text == "quick")
                {
                    reply.Text = "test";
                    reply.SuggestedActions = new SuggestedActions()
                    {
                        Actions = new List<CardAction>()
                        {
                            new CardAction(){Title="USD", Type=ActionTypes.ImBack, Value="USD"},
                            new CardAction(){Title="連結", Type=ActionTypes.OpenUrl, Value="www.google.com"},
                        }
                    };
                }
       
                else if (activity.ChannelId == "facebook")
                {
                        //讀fb data
                     var fbData = JsonConvert.DeserializeObject<FBChannelModel>(activity.ChannelData.ToString());
                     if (fbData.postback != null && fbData.postback.payload.StartsWith("Analyze"))
                     {
                         var url = fbData.postback.payload.Split('>')[1];

                         //vision
                          
                         VisionServiceClient client = new VisionServiceClient("786ccca1c75d434dbbffd67a8194942b", "https://westcentralus.api.cognitive.microsoft.com/vision/v1.0");
                         var result = await client.AnalyzeImageAsync(url, new VisualFeature[] { VisualFeature.Description });
                         reply.Text = result.Description.Captions.First().Text;
                         
                      }

                     else if (fbData.message.quick_reply != null)
                     {
                        reply.Text = $"your choice is {fbData.message.quick_reply.payload}";
                     }
                     else
                     {
                        reply.Text = await ProcessLUIS(activity.Text);
                    
                     }
                }

                /*else
                {
                    reply.Text = $"echo:{activity.Text}";
                }*/
                await connector.Conversations.ReplyToActivityAsync(reply);

            }
            /*else
            {
                HandleSystemMessage(activity);
            }*/

            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private void HandleSystemMessage(Activity activity)
        {
            throw new NotImplementedException();
        }

       
        private async Task<string> ProcessLUIS(string text)
        {
            using (LuisClient client = new LuisClient("735a07a9-5991-4a1c-9ac0-5120a4ee2283", "7b1b7ade14834b58a3fcd24f79190c7d"))
            {
                var result = await client.Predict(text);

                if (result.Intents.Count() <= 0 || result.TopScoringIntent.Name != "查匯率")
                {
                    return "你剛剛打 "+text;
          
                }

                if (result.Entities == null || !result.Entities.Any(x => x.Key.StartsWith("幣別")))
                {
                    return "目前只支援美金和日圓";
                }
             
                //看查甚麼幣別 ? null reference check有東西才繼續
                var currency = result.Entities?.Where(x => x.Key.StartsWith("幣別"))?.First().Value[0].Value;
                // ask api
                return  $"查詢的外幣是{currency}, 價格是30.0";
                
            }


        }

        private void ImageTemplate(Activity reply, string url)
        {
            List<Attachment> att = new List<Attachment>();
            att.Add(new HeroCard() //建立fb ui格式的api
            {
                Title = "Cognitive services",
                Subtitle = "Select from below",
                Images = new List<CardImage>() { new CardImage(url) },
                Buttons = new List<CardAction>()
                {
                    new CardAction(ActionTypes.PostBack, "辨識圖片", value: $"Analyze>{url}")//帶json payload
                }
            }.ToAttachment());

            reply.Attachments = att;
        }
    }
}