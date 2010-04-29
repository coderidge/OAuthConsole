﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Web;
using SevenDigital.Security.OAuth.Signature;

namespace OAuthSig
{
	public class ApiPostRequestBuilder
	{
		public string Build(bool oAuthSignRequest, Uri fullyQualifiedUrl, string postParams,
							string oAuthConsumerKey, string oAuthConsumerSecret) {
			return Build(oAuthSignRequest, fullyQualifiedUrl, postParams, oAuthConsumerKey,
				  oAuthConsumerSecret, String.Empty, String.Empty);
		}

		public string Build(bool oAuthSignRequest, Uri fullyQualifiedUrl, string postParams,
							string oAuthConsumerKey, string oAuthConsumerSecret,
							string oAuthTokenKey,
							string oAuthTokenSecret) {

			WebClient client = new WebClient();

			if (oAuthSignRequest) {
				client.Headers.Add("Authorization",
								   GetSignedAuthorizationHeader(fullyQualifiedUrl, postParams,
																oAuthConsumerSecret,
																oAuthConsumerKey, oAuthTokenKey,
																oAuthTokenSecret));
			}
			else {
				IDictionary<string, string> values = new Dictionary<string, string>();
				values.Add(OAuthQueryParameters.CONSUMER_KEY_KEY, oAuthConsumerKey);
				client.Headers.Add("Authorization", BuildOAuthHeaderString(values));
			}
			client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

			ApiTestHelper.DumpNameValueCollection(client.Headers, "Headers");
			ApiTestHelper.FireLogMessage("[Invoke POST]: {0}", fullyQualifiedUrl);
			ApiTestHelper.FireLogMessage("[Invoke POST-parameters]: {0}", postParams);
			ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback
				(
				(sender, certificate, chain, sslpolicyerrors) => true);

			try {
				return client.UploadString(fullyQualifiedUrl, postParams);
			}
			catch (WebException ex) {
				var reponse = ex.Response;
				using (var reader = new StreamReader(reponse.GetResponseStream())) {
					ApiTestHelper.FireLogMessage("[Invoke POST-Error]: {0}", reader.ReadToEnd());
				}
				return "[Failed: Please check the Console Out Tab]";
			}
		}

		private void AddTo(string key, string parameter,
						   IDictionary<string, string> dictionary) {
			if (false == String.IsNullOrEmpty(parameter)) {
				dictionary.Add(key, HttpUtility.UrlEncode(parameter));
			}
		}

		private string BuildOAuthHeaderString(IDictionary<string, string> oAUthParameters) {
			StringBuilder sb = new StringBuilder();
			sb.Append("OAuth ");
			foreach (var kvp in oAUthParameters) {
				sb.AppendFormat("{0}=\"{1}\",", kvp.Key, kvp.Value);
			}
			sb.Remove(sb.Length - 1, 1);
			return sb.ToString();
		}

		private Dictionary<string, string> GetFormVariables(string postParams) {
			if (string.IsNullOrEmpty(postParams)) {
				return new Dictionary<string, string>();
			}
			IEnumerable<string> enumerable = postParams.Split('&').Select(s => s);
			return enumerable.ToDictionary(str => str.Split('=')[0], str => str.Split('=')[1]);
		}

		private string GetHeader(string oAuthVersion, string nonce, string timeStamp,
								 string signature, string oAuthConsumerKey,
								 string oAuthTokenKey) {
			IDictionary<string, string> oAuthParameters = new Dictionary<string, string>();
			AddTo(OAuthQueryParameters.VERSION_KEY, oAuthVersion, oAuthParameters);
			AddTo(OAuthQueryParameters.NONCE_KEY, nonce, oAuthParameters);
			AddTo(OAuthQueryParameters.TIMESTAMP_KEY, timeStamp, oAuthParameters);
			AddTo(OAuthQueryParameters.SIGNATURE_METHOD_KEY,
				  OAuthQueryParameters.HMACSHA1_SIGNATURE_TYPE, oAuthParameters);
			AddTo(OAuthQueryParameters.CONSUMER_KEY_KEY, oAuthConsumerKey, oAuthParameters);
			AddTo(OAuthQueryParameters.SIGNATURE_KEY, signature, oAuthParameters);

			if (!String.IsNullOrEmpty(oAuthTokenKey)) {
				AddTo(OAuthQueryParameters.TOKEN_KEY, oAuthTokenKey, oAuthParameters);
			}
			return BuildOAuthHeaderString(oAuthParameters);
		}

		private string GetSignature(Uri url, Dictionary<string, string> dictionary,
									out string nonce, out string oAuthVersion,
									out string signature, string oAuthConsumerSecret,
									string oAuthConsumerKey, string oAuthTokenKey,
									string oAuthTokenSecret) {
			SignatureGenerator signatureGenerator = new SignatureGenerator();

			nonce = new DefaultNoncefactory().New();
			oAuthVersion = "1.0";
			string timeStamp = new DefaultTimestampFactory().New();

			signature = signatureGenerator.Generate(url, dictionary, oAuthConsumerKey,
													oAuthConsumerSecret, oAuthTokenKey,
													oAuthTokenSecret, "POST", timeStamp, nonce,
													SignatureGenerator.SignatureTypes.HmacSha1,
													oAuthVersion);
			return timeStamp;
		}

		private string GetSignedAuthorizationHeader(Uri url, string postParams,
													string oAuthConsumerSecret,
													string oAuthConsumerKey,
													string oAuthTokenKey,
													string oAuthTokenSecret) {
			Dictionary<string, string> dictionary = GetFormVariables(postParams);
			string nonce;
			string oAuthVersion;
			string signature;
			string timeStamp = GetSignature(url, dictionary, out nonce, out oAuthVersion,
											out signature, oAuthConsumerSecret,
											oAuthConsumerKey, oAuthTokenKey, oAuthTokenSecret);

			string header = GetHeader(oAuthVersion, nonce, timeStamp, signature,
									  oAuthConsumerKey, oAuthTokenKey);

			return header;
		}
	}
}