using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using GameDevWare.Serialization;
using com.rtm;
using UnityEngine;

namespace com.test {

	public class TestCase {

		private int _sleepCount;
		private byte[] _fileBytes;
		private RTMClient _client;

		public TestCase(byte[] fileBytes) {

			this._fileBytes = fileBytes;

			this._client = new RTMClient(
                "52.83.245.22:13325",
                1000012,
                654321,
                "3993142515BD88A7156629A3AE550B9B",
                null,
                new Dictionary<string, string>(),
                true,
                20 * 1000,
                true
	        );

	        RTMProcessor processor = this._client.GetProcessor();

			processor.GetEvent().AddListener(RTMConfig.SERVER_PUSH.recvPing, (evd) => {

				MemoryStream jsonStream = new MemoryStream();
                Json.Serialize((Dictionary<string, object>)evd.GetPayload(), jsonStream);

	        	Debug.Log("[PUSH] ping: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
            });

            this._client.GetEvent().AddListener("login", (evd) => {

            	Exception ex = evd.GetException();

            	if (ex != null) {

            		Debug.Log("TestCase connect err: " + ex.Message);
            	} else {

	            	Debug.Log("TestCase connect!");
	            	OnLogin();
            	}
            });

            this._client.GetEvent().AddListener("close", (evd) => {

            	Debug.Log("TestCase closed!");
            });

            this._client.GetEvent().AddListener("error", (evd) => {

            	Debug.Log("TestCase error: " + evd.GetException().Message);
            });

            this._client.Login(null, false);
		}

		public void Close() {

			this._client.Destroy();
		}

		private void OnLogin() {

			long to = 778899;
			long fuid = 778898;
			long gid = 999;
	        long rid = 666;

	        Dictionary<String, String>  attrs = new Dictionary<String, String>();
	        attrs.Add("user1", "test user1 attrs");

	        List<long> tos = new List<long>();
	        tos.Add((long)654321);
	        tos.Add(fuid);
	        tos.Add(to);

	        List<long> friends = new List<long>();
	        friends.Add(fuid);
	        friends.Add(to);

			int timeout = 20 * 1000;
	        int sleep = 1000;

			Debug.Log("test start!");

			this.ThreadSleep(sleep);

			//rtmGate (2)
	        //---------------------------------SendMessage--------------------------------------
	        this._client.SendMessage(to, (byte) 8, "hello !", "", 0, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] SendMessage: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()) + ", mid: " + cbd.GetMid());
	        	} else {

	        		Debug.Log("[ERR] SendMessage: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

			//rtmGate (3)
	        //---------------------------------SendGroupMessage--------------------------------------
	        this._client.SendGroupMessage(gid, (byte) 8, "hello !", "", 0, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] SendGroupMessage: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()) + ", mid: " + cbd.GetMid());
	        	} else {

	        		Debug.Log("[ERR] SendGroupMessage: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (4)
	        //---------------------------------SendRoomMessage--------------------------------------
	        this._client.SendRoomMessage(rid, (byte) 8, "hello !", "", 0, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] SendRoomMessage: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()) + ", mid: " + cbd.GetMid());
	        	} else {

	        		Debug.Log("[ERR] SendRoomMessage: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (5)
	        //---------------------------------GetUnreadMessage--------------------------------------
	        this._client.GetUnreadMessage(timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetUnreadMessage: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetUnreadMessage: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (6)
	        //---------------------------------CleanUnreadMessage--------------------------------------
	        this._client.CleanUnreadMessage(timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] CleanUnreadMessage: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] CleanUnreadMessage: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (7)
	        //---------------------------------GetSession--------------------------------------
	        this._client.GetSession(timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetSession: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetSession: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (8)
	        //---------------------------------GetGroupMessage--------------------------------------
	        this._client.GetGroupMessage(gid, true, 10, 0, 0, 0, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetGroupMessage: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetGroupMessage: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (9)
	        //---------------------------------GetRoomMessage--------------------------------------
	        this._client.GetRoomMessage(rid, true, 10, 0, 0, 0, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetRoomMessage: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetRoomMessage: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (10)
	        //---------------------------------GetBroadcastMessage--------------------------------------
	        this._client.GetBroadcastMessage(true, 10, 0, 0, 0, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetBroadcastMessage: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetBroadcastMessage: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (11)
	        //---------------------------------GetP2PMessage--------------------------------------
	        this._client.GetP2PMessage(to, true, 10, 0, 0, 0, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetP2PMessage: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetP2PMessage: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (12)
	        //---------------------------------FileToken--------------------------------------
	        this._client.FileToken("sendfile", null, to, 0, 0, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] FileToken: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] FileToken: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (14)
	        //---------------------------------AddAttrs--------------------------------------
	        this._client.AddAttrs(attrs, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] AddAttrs: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] AddAttrs: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (15)
	        //---------------------------------GetAttrs--------------------------------------
	        this._client.GetAttrs(timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetAttrs: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetAttrs: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (16)
	        //---------------------------------AddDebugLog--------------------------------------
	        this._client.AddDebugLog("msg", "attrs", timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] AddDebugLog: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] AddDebugLog: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (17)
	        //---------------------------------AddDevice--------------------------------------
	        this._client.AddDevice("app-info", "device-token", timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] AddDevice: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] AddDevice: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (18)
	        //---------------------------------RemoveDevice--------------------------------------
	        this._client.RemoveDevice("device-token", timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] RemoveDevice: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] RemoveDevice: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (19)
	        //---------------------------------SetTranslationLanguage--------------------------------------
	        this._client.SetTranslationLanguage("en", timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] SetTranslationLanguage: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] SetTranslationLanguage: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (20)
	        //---------------------------------Translate--------------------------------------
	        this._client.Translate("你好!", null, "en", timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] Translate: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] Translate: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (21)
	        //---------------------------------AddFriends--------------------------------------
	        this._client.AddFriends(friends, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] AddFriends: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] AddFriends: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (22)
	        //---------------------------------DeleteFriends--------------------------------------
	        this._client.DeleteFriends(friends, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] DeleteFriends: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] DeleteFriends: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (23)
	        //---------------------------------GetFriends--------------------------------------
	        this._client.GetFriends(timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetFriends: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetFriends: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (24)
	        //---------------------------------AddGroupMembers--------------------------------------
	        this._client.AddGroupMembers(gid, tos, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] AddGroupMembers: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] AddGroupMembers: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (25)
	        //---------------------------------DeleteGroupMembers--------------------------------------
	        this._client.DeleteGroupMembers(rid, tos, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] DeleteGroupMembers: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] DeleteGroupMembers: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (26)
	        //---------------------------------GetGroupMembers--------------------------------------
	        this._client.GetGroupMembers(gid, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetGroupMembers: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetGroupMembers: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (27)
	        //---------------------------------GetUserGroups--------------------------------------
	        this._client.GetUserGroups(timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetUserGroups: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetUserGroups: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (28)
	        //---------------------------------EnterRoom--------------------------------------
	        this._client.EnterRoom(rid, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] EnterRoom: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] EnterRoom: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (29)
	        //---------------------------------LeaveRoom--------------------------------------
	        this._client.LeaveRoom(rid, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] LeaveRoom: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] LeaveRoom: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (30)
	        //---------------------------------GetUserRooms--------------------------------------
	        this._client.GetUserRooms(timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetUserRooms: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetUserRooms: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (31)
	        //---------------------------------GetOnlineUsers--------------------------------------
	        this._client.GetOnlineUsers(tos, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] GetOnlineUsers: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] GetOnlineUsers: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (32)
	        //---------------------------------DeleteMessage--------------------------------------
	        this._client.DeleteMessage(0, to, (byte)1, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] DeleteMessage: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] DeleteMessage: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (33)
	        //---------------------------------Kickout--------------------------------------
	        this._client.Kickout("", timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] Kickout: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] Kickout: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (35)
	        //---------------------------------DBSet--------------------------------------
	        this._client.DBSet("db-test-key", "db-test-value", timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] DBSet: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] DBSet: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (34)
	        //---------------------------------DBGet--------------------------------------
	        this._client.DBGet("db-test-key", timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] DBGet: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] DBGet: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //fileGate (1)
	        //---------------------------------SendFile--------------------------------------
	        this._client.SendFile((byte)50, to, this._fileBytes, 0, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] SendFile: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] SendFile: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //fileGate (3)
	        //---------------------------------SendGroupFile--------------------------------------
	        this._client.SendGroupFile((byte)50, gid, this._fileBytes, 0, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] SendGroupFile: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] SendGroupFile: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //fileGate (4)
	        //---------------------------------SendRoomFile--------------------------------------
	        this._client.SendRoomFile((byte)50, rid, this._fileBytes, 0, timeout, (cbd) => {

	        	object obj = cbd.GetPayload();

	        	if (obj != null) {

	        		Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    MemoryStream jsonStream = new MemoryStream();
                    Json.Serialize(dict, jsonStream);

	        		Debug.Log("[DATA] SendRoomFile: " + System.Text.Encoding.UTF8.GetString(jsonStream.ToArray()));
	        	} else {

	        		Debug.Log("[ERR] SendRoomFile: " + cbd.GetException().Message);
	        	}
	        });

	        this.ThreadSleep(sleep);

	        //rtmGate (13)
	        //---------------------------------Close--------------------------------------
	        // this._client.Close();

	        Debug.Log("test end! " + (this._sleepCount - 1));
		}

		private void ThreadSleep(int ms) {

			Thread.Sleep(ms);
	        this._sleepCount++;
	    }
	} 
}