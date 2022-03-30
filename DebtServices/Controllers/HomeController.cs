using DebtServices.Models;
using DebtServices.Models.Configurations;
using DebtServices.Services;
using DebtServices.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Tencent;

namespace DebtServices.Controllers
{
    [Route("/")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        private readonly WeComConfiguration WeComConfiguration;

        private readonly WeComService WeComService;

        private readonly ILogger<HomeController> Logger;

        private readonly WXBizMsgCrypt WxCpt;

        public HomeController(IOptions<WeComConfiguration> weComConfiguration, ILogger<HomeController> logger, WeComService weComService)
        {
            WeComConfiguration = weComConfiguration.Value;
            WeComService = weComService;
            Logger = logger;
            WxCpt = new WXBizMsgCrypt(
                WeComConfiguration.Message.Token,
                WeComConfiguration.Message.EncodingAESKey,
                WeComConfiguration.CorpId);
        }

        [HttpGet("/")]
        public ActionResult<string> VerifyUrl(string msg_signature, string timestamp, string nonce, string echostr)
        {
            Logger.LogInformation($"HOME: VERIFY_URL, MSG_SIGNATURE: {msg_signature} TIMESTAMP: {timestamp} NONCE: {nonce}");

            string sEchoStr = "";
            var verifyRet = WxCpt.VerifyURL(msg_signature, timestamp, nonce, echostr, ref sEchoStr);

            if (verifyRet != 0)
            {
                string failMessage = $"HOME: VERIFY_URL ERR: VERIFY_FAIL: {verifyRet}";
                Logger.LogError(failMessage);
                return Ok(failMessage);
            }

            Logger.LogInformation($"HOME: VERIFY_URL, RESPONSE: {sEchoStr}");
            return Ok(sEchoStr);
        }

        [HttpPost("/")]
        public async Task<ActionResult<string>> ReceiveMessageAsync([FromQuery] string msg_signature, [FromQuery] string timestamp, [FromQuery] string nonce)
        {
            Logger.LogInformation($"HOME: RECEIVE_MESSAGE, MSG_SIGNATURE: {msg_signature} TIMESTAMP: {timestamp} NONCE: {nonce}");

            // Read body
            string receivedBodyString = await new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync();

            // Decrypt body
            string decryptedBodyString = "";
            var decryptMsgRet = WxCpt.DecryptMsg(msg_signature, timestamp, nonce, receivedBodyString, ref decryptedBodyString);
            if (decryptMsgRet != 0)
            {
                string decryptMsgFailString = $"HOME: RECEIVE_MESSAGE ERR: DECRYPT_FAIL: {decryptMsgRet}";
                Logger.LogError(decryptMsgFailString);
                return Ok(decryptMsgFailString);
            }
            Logger.LogInformation($"HOME: RECEIVE_MESSAGE, DECRYPTED:\n{decryptedBodyString}");

            // Deserialize body
            WeComReceiveMessage receivedMessage = XmlUtilities.Deserialize<WeComReceiveMessage>(decryptedBodyString);
            if (receivedMessage == null)
            {
                Logger.LogError("HOME: RECEIVE_MESSAGE ERR: INVALID MESSAGE");
                return BadRequest("RECEIVE_MESSAGE ERR: INVALID MESSAGE");
            }

            // Build response
            WeComInstanceReply replyMessage = await WeComService.ReplyMessageAsync(receivedMessage);

            if (replyMessage == null)
            {
                return Ok("");
            }

            // Serialize response
            string replyBodyString = XmlUtilities.Serialize(replyMessage);
            Logger.LogInformation($"HOME: RECEIVE_MESSAGE, REPLY:\n{replyBodyString}");

            // Encrypt response
            string encryptedBodyString = "";
            var encryptMsgRet = WxCpt.EncryptMsg(replyBodyString, timestamp, nonce, ref encryptedBodyString);

            if (encryptMsgRet != 0)
            {
                string encryptFailMessage = $"HOME: RECEIVE_MESSAGE ERR: ENCRYPT_FAIL: {encryptMsgRet}";
                Logger.LogError(encryptFailMessage);
                return Ok(encryptFailMessage);
            }

            // Return response
            return Ok(encryptedBodyString);
        }
    }
}
