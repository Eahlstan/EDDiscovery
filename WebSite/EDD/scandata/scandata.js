/*
 * Copyright 2021-2021 Robbyxp1 @ github.com
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 */

import { WriteHeader, WriteNav, WriteFooter } from "/header.js"
import { WSURIFromLocation } from "/jslib/websockets.js"
import { ShowPopup } from "/jslib/popups.js"
import { CreateDiv, CreateImage } from "/jslib/elements.js"
import { RequestScanData, FillScanTable } from "/scans/scans.js"
import { RequestStatus, FillSystemTable } from "/systemtable/systemtable.js"
import { FetchState, StoreState } from "/jslib/localstorage.js"
import { WriteMenu, ToggleMenu, GetMenuItemCheckState, CloseMenus } from "/jslib/menus.js"

var websocket;

function OnLoad()
{
    var header = document.getElementsByTagName("header");
    WriteHeader(header[0]);
    var nav = document.getElementsByTagName("nav");
    WriteNav(nav[0], 2);

    var div = CreateDiv("menubutton", "menubutton1");

    div.appendChild(CreateImage("/Images/menu.png", "Menu", null, togglemenu, null, null, "menubutton"));

    WriteMenu(div, "scanmenu", "navmenu", [
        ["checkbox", "materials", "Show materials", scanmenuchange, false],
        ["checkbox", "value", "Show Value", scanmenuchange, true],
        ["checkbox", "EDSM", "Check EDSM", scanmenuchange, false]
    ]);

    nav[0].appendChild(div);

    var footer = document.getElementsByTagName("footer");
    WriteFooter(footer[0], null);

    var uri = WSURIFromLocation()
    console.log("WS URI:" + uri);
    websocket = new WebSocket(uri, "EDDJSON");
	websocket.onopen = function (evt) { onOpen(evt) };
	websocket.onclose = function (evt) { onClose(evt) };
	websocket.onmessage = function (evt) { onMessage(evt) };
    websocket.onerror = function (evt) { onError(evt) };
}

document.body.onload = OnLoad;

function onOpen(evt)
{
    RequestStatus(websocket,-1);
    var edsm = GetMenuItemCheckState("scanmenu", "EDSM");
    RequestScanData(websocket, -1, edsm);
}

function onClose(evt)
{
    ShowPopup("lostconnection");
}

function onError(evt)
{
    console.log("Web Error " + evt.data);
    ShowPopup("lostconnection");
}


var lastscandata;       // keep last scan data

function onMessage(evt)
{
	var jdata = JSON.parse(evt.data);

    if (jdata.responsetype == "status")    // we requested a status or status was pushed, update screen
    {
       // console.log("scandata status " + evt.data);
        FillSystemTable(jdata);
    }
    else if (jdata.responsetype == "scandata")    // we requested a status or status was pushed, update screen
    {
        console.log("New scandata received");
        lastscandata = jdata;
        FillScan();
    }
    else if (jdata.responsetype == "scandatachanged")    // system notified scan data changed
    {
        console.log("scandata informed changed");
        var edsm = GetMenuItemCheckState("scanmenu", "EDSM");
        RequestScanData(websocket,-1,edsm);
    }
}

function FillScan()
{
    var showmaterials = GetMenuItemCheckState("scanmenu", "materials");
    var showvalue = GetMenuItemCheckState("scanmenu", "value");
    var edsm = GetMenuItemCheckState("scanmenu", "EDSM");

    //  console.log("scandata stars " + evt.data);
    FillScanTable(lastscandata, showmaterials, showvalue, edsm);
}

function scanmenuchange(mouseevent)
{
    var ct = mouseevent.currentTarget;
    console.log("MI " + ct.id + " tag " + ct.tag);
    if (ct.tag != null)
        StoreState(ct.tag, ct.checked);
    CloseMenus();
    FillScan();
}

function togglemenu()
{
    ToggleMenu("scanmenu");
}