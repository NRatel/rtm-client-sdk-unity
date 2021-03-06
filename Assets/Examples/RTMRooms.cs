﻿using System.Collections.Generic;
using UnityEngine;
using com.fpnn.rtm;

class Rooms : Main.ITestCase
{
    private static long roomId = 556677;

    private RTMClient client;

    public void Start(string endpoint, long pid, long uid, string token)
    {
        client = LoginRTM(endpoint, pid, uid, token);

        if (client == null)
        {
            Debug.Log("User " + uid + " login RTM failed.");
            return;
        }

        Debug.Log("======== enter room =========");
        EnterRoom(client, roomId);

        Debug.Log("======== get self rooms =========");
        GetSelfRooms(client);

        Debug.Log("======== leave room =========");
        LeaveRoom(client, roomId);

        Debug.Log("======== get self rooms =========");
        GetSelfRooms(client);


        Debug.Log("======== enter room =========");
        EnterRoom(client, roomId);

        Debug.Log("======== set room infos =========");

        SetRoomInfos(client, roomId, "This is public info", "This is private info");
        GetRoomInfos(client, roomId);

        Debug.Log("======== change room infos =========");

        SetRoomInfos(client, roomId, "", "This is private info");
        GetRoomInfos(client, roomId);

        Debug.Log("======== change room infos =========");

        SetRoomInfos(client, roomId, "This is public info", "");
        GetRoomInfos(client, roomId);

        Debug.Log("======== only change the private infos =========");

        SetRoomInfos(client, roomId, null, "balabala");
        GetRoomInfos(client, roomId);

        SetRoomInfos(client, roomId, "This is public info", "This is private info");
        client.Bye();

        Debug.Log("======== user relogin =========");

        client = LoginRTM(endpoint, pid, uid, token);

        if (client == null)
            return;

        Debug.Log("======== enter room =========");
        EnterRoom(client, roomId);

        GetRoomInfos(client, roomId);

        Debug.Log("============== Demo completed ================");
    }

    public void Stop() { }

    static RTMClient LoginRTM(string rtmEndpoint, long pid, long uid, string token)
    {
        RTMClient client = new RTMClient(rtmEndpoint, pid, uid, new example.common.RTMExampleQuestProcessor());

        int errorCode = client.Login(out bool ok, token);
        if (ok)
        {
            Debug.Log("RTM login success.");
            return client;
        }
        else
        {
            Debug.Log("RTM login failed, error code: " + errorCode);
            return null;
        }
    }

    static void EnterRoom(RTMClient client, long roomId)
    {
        int errorCode = client.EnterRoom(roomId);
        if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
            Debug.Log("Enter room " + roomId + " in sync failed.");
        else
            Debug.Log("Enter room " + roomId + " in sync successed.");
    }

    static void LeaveRoom(RTMClient client, long roomId)
    {
        int errorCode = client.LeaveRoom(roomId);
        if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
            Debug.Log("Leave room " + roomId + " in sync failed.");
        else
            Debug.Log("Leave room " + roomId + " in sync successed.");
    }

    static void GetSelfRooms(RTMClient client)
    {
        int errorCode = client.GetUserRooms(out HashSet<long> rids);

        if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
            Debug.Log("Get user rooms in sync failed, error code is " + errorCode);
        else
        {
            Debug.Log("Get user rooms in sync successed, current I am in " + rids.Count + " rooms.");
            foreach (long rid in rids)
                Debug.Log("-- room id: " + rid);
        }
    }

    static void SetRoomInfos(RTMClient client, long roomId, string publicInfos, string privateInfos)
    {
        int errorCode = client.SetRoomInfo(roomId, publicInfos, privateInfos);

        if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
            Debug.Log("Set room infos in sync failed, error code is " + errorCode);
        else
            Debug.Log("Set room infos in sync successed.");
    }

    static void GetRoomInfos(RTMClient client, long roomId)
    {
        int errorCode = client.GetRoomInfo(out string publicInfos, out string privateInfos, roomId);

        if (errorCode != com.fpnn.ErrorCode.FPNN_EC_OK)
            Debug.Log("Get room infos in sync failed, error code is " + errorCode);
        else
        {
            Debug.Log("Get room infos in sync successed.");
            Debug.Log("Public info: " + (publicInfos ?? "null"));
            Debug.Log("Private info: " + (privateInfos ?? "null"));
        }
    }
}