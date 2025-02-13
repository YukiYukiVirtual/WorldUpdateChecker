/*
MIT License

Copyright © 2025 YukiYukiVirtual

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace YukiYukiVirtual
{
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
	public class WorldUpdateChecker : UdonSharpBehaviour
	{
		[SerializeField] private VRCUrl url = new VRCUrl("https://api.vrchat.cloud/api/1/worlds/");
		[SerializeField] private UdonBehaviour targetUdonBehaviour;
		[SerializeField] private string targetUdonMethodName;
		// ロードできない人のフラグ
		private bool couldNotLoadUrl = false;
		// インスタンス作成時刻
		// -1: 未初期化
		// -2: バージョン不一致
		// else: インスタンス作成時刻設定済み
		[UdonSynced(UdonSyncMode.None)] private long instanceTicks = -1;
		
		private readonly string log_prefix = "[YukiYukiVirtual/WorldUpdateChecker]";
		void Start()
		{
			// パラメータチェック
			if(url.ToString().IndexOf("https://api.vrchat.cloud/api/1/worlds/wrld_") == -1) Debug.LogError($"{log_prefix} Invalid Url {url}");
			if(targetUdonBehaviour == null) Debug.LogError($"{log_prefix} targetUdonBehaviour is null");
			if(String.IsNullOrWhiteSpace(targetUdonMethodName)) Debug.LogError($"{log_prefix} targetUdonMethodName is null");
		}
		public void NotifyAllPlayer()
		{
			Debug.Log($"{log_prefix} NotifyAllPlayer");
			if(targetUdonBehaviour != null)
			{
				targetUdonBehaviour.SendCustomEvent(targetUdonMethodName);
			}
		}
		public void load()
		{
			// ロードできなかった人はロードできないようにする。
			if(!couldNotLoadUrl)				
			{
				VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
			}
		}
		public override void OnPlayerJoined(VRCPlayerApi player)
		{
			if(Networking.GetOwner(this.gameObject) == Networking.LocalPlayer)
			{
				// 初期化
				if(instanceTicks == -1)
				{
					// インスタンスが建った時刻を同期変数に入れる
					DateTime dt = Networking.GetNetworkDateTime();
					instanceTicks = dt.Ticks;
					RequestSerialization(); // 同期
					Debug.Log($"{log_prefix} DateTime: instanceTicks: {dt.ToString()} {dt.Kind} ({instanceTicks})");
				}
				// バージョン不一致
				else if(instanceTicks == -2)
				{
					SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(NotifyAllPlayer));
				}
				// 更新チェック
				else
				{
					// Ownerのやる気がないなら他の人に投げる
					// インスタンス中の全員がロードできなければ何もできないが、新しくJoinしてきた人がロードできればその人がOwnerになる
					if(couldNotLoadUrl)
					{
						SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(load));
					}
					else
					{
						load();
					}
				}
			}
		}
		public override void OnStringLoadSuccess(IVRCStringDownload result)
		{
			// ロードに成功した人にOwnerが移る(Ownerならダウンロードに成功するはずという状態にできる)
			// ぶっちゃけ誰でもいい
			if(Networking.GetOwner(this.gameObject) != Networking.LocalPlayer)
			{
				Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
			}
			// JSONとして読み込む
			if(VRCJson.TryDeserializeFromJson(result.Result, out DataToken dataToken))
			{
				// 正しく読み込めた
				if(dataToken.TokenType == TokenType.DataDictionary)
				{
					// 更新日時を取り出す
					if(dataToken.DataDictionary.TryGetValue("updated_at", out DataToken value))
					{
						// 更新日時(UTC)をTicksにする
						DateTime dt = DateTime.Parse(value.ToString()).ToUniversalTime();
						long updatedTicks = dt.Ticks;
						Debug.Log($"{log_prefix} DateTime: updatedTicks: {dt.ToString()} {dt.Kind} ({updatedTicks})");
						// バージョン初期化がまだ
						if(instanceTicks == -1)
						{
							Debug.LogError($"{log_prefix} instanceTicks is not initialized.");
						}
						// ワールド更新日時がインスタンスより後になっている
						else if(instanceTicks < updatedTicks)
						{
							// バージョン不一致を全体に通知する
							instanceTicks = -2;
							SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(NotifyAllPlayer));
							RequestSerialization();
						}
					}
					else
					{
						Debug.LogError($"{log_prefix} Failed to get version - {value.ToString()} {result.Result}");
					}
				}
				else
				{
					Debug.LogError($"{log_prefix} TokenType is not {TokenType.DataDictionary}: {dataToken.TokenType} {result.Result}");
				}
			}
			else
			{
				Debug.LogError($"{log_prefix} Failed to Deserialize json {result.Result} - {dataToken.ToString()}");
			}
		}
		public override void OnStringLoadError(IVRCStringDownload result)
		{
			Debug.LogError($"{log_prefix} Error loading string: {result.ErrorCode} - {result.Error}");
			couldNotLoadUrl = true;
			// 信頼されていないURLを許可しない設定になっている場合は、401が返ってくる。
			// 自分はロードできないので他の人に任せる
			SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(load));
		}
	}
}
