﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text;
using Utilities.Utilities;
using WeComCommon.Models;
using WeComCommon.Models.Configurations;
using WeComCommon.Services;
using WeComCommon.Tencent;

namespace WeComMessageService.Controllers
{
    [Route("/")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        private readonly WeComServicesConfiguration WeComConfiguration;

        private readonly ILogger<HomeController> Logger;

        private readonly WeComService WeComService;

        private readonly IMemoryCache MemoryCache;

        private static Dictionary<string, WXBizMsgCrypt> WXBizMsgCrypts = null;
        private static object CryptsInitializeLock = new object();
        
        // Dedupe cache expiration time - 1 minute
        private static readonly TimeSpan MessageDedupeWindow = TimeSpan.FromMinutes(1);

        public HomeController(IOptions<WeComServicesConfiguration> weComConfiguration, ILogger<HomeController> logger, WeComService weComService, IMemoryCache memoryCache)
        {
            WeComConfiguration = weComConfiguration.Value;
            WeComService = weComService;
            Logger = logger;
            MemoryCache = memoryCache;

            if (WXBizMsgCrypts == null)
            {
                Logger.LogInformation("HOMESERVICE: Initialize crypts...");
                lock (CryptsInitializeLock)
                {
                    WXBizMsgCrypts = new Dictionary<string, WXBizMsgCrypt>();
                    foreach (var appConfiguration in WeComConfiguration.AppConfigurations)
                    {
                        Logger.LogInformation($"HOMESERVICE: Create crypts for {appConfiguration.AppId}...");
                        WXBizMsgCrypts[appConfiguration.AppId] = new WXBizMsgCrypt(appConfiguration.Message.Token, appConfiguration.Message.EncodingAESKey, appConfiguration.CorpId);
                    }
                }
            }
        }

        private WXBizMsgCrypt GetCryptFromRequest(HttpRequest httpRequest)
        {
            httpRequest.Headers.TryGetValue("X-App-Id", out var appIds);
            string appId = appIds.Count > 0 ? string.Join(string.Empty, appIds): string.Empty;
            Logger.LogInformation($"HOMESERVICE: Get request to {appId}");
            return WXBizMsgCrypts.First(x => string.Equals(appId, x.Key, StringComparison.OrdinalIgnoreCase)).Value;
        }

        [HttpGet("/")]
        public ActionResult<string> VerifyUrl(string msg_signature, string timestamp, string nonce, string echostr)
        {
            Logger.LogInformation($"HOME: VERIFY_URL, MSG_SIGNATURE: {msg_signature} TIMESTAMP: {timestamp} NONCE: {nonce}");

            string sEchoStr = "";

            var wxCrypt = GetCryptFromRequest(Request);
            var verifyRet = wxCrypt.VerifyURL(msg_signature, timestamp, nonce, echostr, ref sEchoStr);

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

            // Check for duplicate message
            string messageKey = $"{msg_signature}:{timestamp}:{nonce}";
            if (MemoryCache.TryGetValue(messageKey, out _))
            {
                Logger.LogInformation($"HOME: RECEIVE_MESSAGE, DUPLICATE DETECTED: {messageKey}");
                return Ok("success"); // Return success without processing to avoid duplicate processing
            }

            // Add to cache to mark this message as processed
            MemoryCache.Set(messageKey, true, MessageDedupeWindow);

            // Read body
            string receivedBodyString = await new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync();

            // Decrypt body
            string decryptedBodyString = "";
            var wxCrypt = GetCryptFromRequest(Request);
            var decryptMsgRet = wxCrypt.DecryptMsg(msg_signature, timestamp, nonce, receivedBodyString, ref decryptedBodyString);
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
            var encryptMsgRet = wxCrypt.EncryptMsg(replyBodyString, timestamp, nonce, ref encryptedBodyString);

            if (encryptMsgRet != 0)
            {
                string encryptFailMessage = $"HOME: RECEIVE_MESSAGE ERR: ENCRYPT_FAIL: {encryptMsgRet}";
                Logger.LogError(encryptFailMessage);
                return Ok(encryptFailMessage);
            }

            // Return response
            return Ok(encryptedBodyString);
        }

#if DEBUG
        [HttpPost("/plain")]
        public async Task<ActionResult<string>> ReceivePlainMessageAsync()
        {
            // Read body
            string receivedBodyString = await new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync();

            // Deserialize body
            WeComReceiveMessage receivedMessage = XmlUtilities.Deserialize<WeComReceiveMessage>(receivedBodyString);
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
            return Ok(replyBodyString);
        }
#endif
    }
}
