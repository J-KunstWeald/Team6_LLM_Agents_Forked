using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using com.csutil.http.apis;
using com.csutil.model.jsonschema;
using Newtonsoft.Json.Linq;
using Xunit;
using com.csutil.injection;
using com.csutil.io;
using com.csutil;
using static com.csutil.integrationTests.http.OpenAiTests; //needed for YesNoResponse Class

namespace com.csutil.integrationTests.http
{
    public class ChatGptTests
    {
        private static string OpenAiKey = "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";

        public ChatGptTests(Xunit.Abstractions.ITestOutputHelper logger) { logger.UseAsLoggingOutput(); }

        private static ChatGpt.Request NewGpt4JsonRequestWithFullConversation(List<ChatGpt.Line> conversationSoFar)
        {
            var request = new ChatGpt.Request(conversationSoFar);
            // Use json as the response format:
            request.response_format = ChatGpt.Request.ResponseFormat.json;
            request.model = "gpt-4-1106-preview"; // See https://platform.openai.com/docs/models/gpt-4
            return request;
        }

        [Fact]
        public static async Task TaskOne() {
            var openAi = new OpenAi(OpenAiKey);
            
            var messagesAgentIsItAnimal = new List<ChatGpt.Line>();
            messagesAgentIsItAnimal.Add(new ChatGpt.Line(ChatGpt.Role.system, content: "You are a helpful assistant designed to output JSON."));
            
            var messagesAgentNameAnimal = new List<ChatGpt.Line>();
            messagesAgentNameAnimal.Add(new ChatGpt.Line(ChatGpt.Role.system, content: "You are a helpful assistant designed to output JSON."));
            messagesAgentNameAnimal.Add(new ChatGpt.Line(ChatGpt.Role.user, content: "Name an Animal"));

            { // The user inputs a question but the response should be automatically parsable as a YesNoResponse:

                // Create an example object so that the AI knows how the response json should look like for user inputs:
                var yesNoResponseFormat = new YesNoResponse()
                {
                    confidence = 100,
                    inputQuestionInterpreted = "Is the sky blue?",
                    yesNoAnswer = true,
                    explanation = "The sky is blue because of the way the atmosphere interacts with sunlight."
                };
                
                var responseAgent2 = await openAi.ChatGpt(NewGpt4JsonRequestWithFullConversation(messagesAgentNameAnimal));
                ChatGpt.Line newLinePotentialAnimal = responseAgent2.choices.Single().message;
                messagesAgentIsItAnimal.Add(newLinePotentialAnimal);

                messagesAgentIsItAnimal.AddUserLineWithJsonResultStructure("Is the input an animal?", yesNoResponseFormat);

                // Send the messages to the AI and get the response:
                var response = await openAi.ChatGpt(NewGpt4JsonRequestWithFullConversation(messagesAgentIsItAnimal));
                ChatGpt.Line newLine = response.choices.Single().message;
                messagesAgentIsItAnimal.Add(newLine);

                // Parse newLine.content as a YesNoResponse:
                var yesNoResponse = newLine.ParseNewLineContentAsJson<YesNoResponse>();

                // Dogs can look up, lets hope the AI knows that too:
                Assert.True(yesNoResponse.yesNoAnswer);
                // Since the input question is very short the interpretation will be the same string:
                //Assert.Equal("Is the input an animal?", yesNoResponse.inputQuestionInterpreted);
                // The AI is very confident in its answer:
                Assert.True(yesNoResponse.confidence > 50);
                // The AI also explains why it gave the answer:
                Assert.NotEmpty(yesNoResponse.explanation);

            }

            Log.d("messages=" + JsonWriter.AsPrettyString(messagesAgentIsItAnimal));

            Log.d("messages=" + JsonWriter.AsPrettyString(messagesAgentNameAnimal));
        }
    }
}