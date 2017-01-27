﻿/**
 * Outlook integration for SuiteCRM.
 * @package Outlook integration for SuiteCRM
 * @copyright SalesAgility Ltd http://www.salesagility.com
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU AFFERO GENERAL PUBLIC LICENSE as published by
 * the Free Software Foundation; either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU AFFERO GENERAL PUBLIC LICENSE
 * along with this program; if not, see http://www.gnu.org/licenses
 * or write to the Free Software Foundation,Inc., 51 Franklin Street,
 * Fifth Floor, Boston, MA 02110-1301  USA
 *
 * @author SalesAgility <info@salesagility.com>
 */
using System;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;
using SuiteCRMClient.Logging;
using SuiteCRMClient.RESTObjects;

namespace SuiteCRMClient
{
    public class clsUsersession
    {
        private readonly ILogger _log;

        public string SuiteCRMUsername { get; set; }
        public string SuiteCRMPassword { get; set; }
        public string LDAPKey { get; set; }
        public string LDAPIV = "password";
        public bool AwaitingAuthentication { get; set; }
        public string id { get; set; }

        public clsUsersession(string URL, string Username, string Password, string strLDAPKey, ILogger log)
        {
            _log = log;
            if (URL != "")
            {
                CrmRestServer.SuiteCRMURL = new Uri(URL);
                SuiteCRMUsername = Username;
                SuiteCRMPassword = Password;
                LDAPKey = strLDAPKey;
            }
            id = "";
        }

        public void Login()
        {
            try
            {
                if (LDAPKey != "" && LDAPKey.Trim().Length != 0)
                {
                    AuthenticateLDAP();
                }
                else
                {
                    AwaitingAuthentication = true;
                    object loginData = new
                    {
                        @user_auth = new
                        {
                            @user_name = SuiteCRMUsername,
                            @password = GetMD5Hash(SuiteCRMPassword)
                        }
                    };
                    var loginReturn = CrmRestServer.GetCrmResponse<RESTObjects.Login>("login", loginData);
                    if (loginReturn.ErrorName != null)
                    {
                        loginData = new
                        {
                            @user_auth = new
                            {
                                @user_name = SuiteCRMUsername,
                                @password = SuiteCRMPassword
                            }
                        };
                        loginReturn = CrmRestServer.GetCrmResponse<RESTObjects.Login>("login", loginData);
                        if (loginReturn.ErrorName != null)
                        {
                            id = "";
                            SuiteCRMClient.clsSuiteCRMHelper.SuiteCRMUserSession = null;
                            throw new Exception(loginReturn.ErrorDescription);
                        }
                        else
                        {
                            id = loginReturn.SessionID;
                            SuiteCRMClient.clsSuiteCRMHelper.SuiteCRMUserSession = this;
                        }
                    }
                    else
                    {
                        id = loginReturn.SessionID;
                        SuiteCRMClient.clsSuiteCRMHelper.SuiteCRMUserSession = this;
                    }
                    AwaitingAuthentication = false;
                }
            }
            catch (Exception ex)
            {
                _log.Error("Login error", ex);
                id = "";
                SuiteCRMClient.clsSuiteCRMHelper.SuiteCRMUserSession = null;
                throw;
            }

        }

        public void AuthenticateLDAP()
        {
            try
            {
                AwaitingAuthentication = true;
                byte[] buffer = new MD5CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(LDAPKey));
                StringBuilder builder = new StringBuilder();
                foreach (byte num in buffer)
                {
                    builder.Append(num.ToString("x2", CultureInfo.InvariantCulture));
                }
                TripleDES edes = new TripleDESCryptoServiceProvider
                {
                    Mode = CipherMode.CBC,
                    Key = Encoding.UTF8.GetBytes(builder.ToString(0, 0x18)),
                    IV = Encoding.UTF8.GetBytes(LDAPIV),
                    Padding = PaddingMode.Zeros
                };
                byte[] buffer2 = edes.CreateEncryptor().TransformFinalBlock(Encoding.UTF8.GetBytes(SuiteCRMPassword), 0, Encoding.UTF8.GetByteCount(SuiteCRMPassword));
                StringBuilder builder2 = new StringBuilder();
                foreach (byte num2 in buffer2)
                {
                    builder2.Append(num2.ToString("x2", CultureInfo.InvariantCulture));
                }
                object loginData = new
                {
                    @user_auth = new
                    {
                        @user_name = SuiteCRMUsername,
                        @password = builder2.ToString()
                    }
                };
                eSetEntryResult _result = SuiteCRMClient.CrmRestServer.GetCrmResponse<eSetEntryResult>("login", loginData);
                if (_result.id == null || _result.id == "")
                {
                    id = "";
                    SuiteCRMClient.clsSuiteCRMHelper.SuiteCRMUserSession = null;
                    return;
                }
                id = _result.id;
                SuiteCRMClient.clsSuiteCRMHelper.SuiteCRMUserSession = this;
                AwaitingAuthentication = false;
            }
            catch (Exception)
            {
                id = "";
                SuiteCRMClient.clsSuiteCRMHelper.SuiteCRMUserSession = null;
                throw;
            }
        }

        public void LogOut()
        {
            try
            {
                if (id != "")
                {
                    object logoutData = new
                    {
                        @session = id
                    };
                    var objRet = CrmRestServer.GetCrmResponse<object>("logout", logoutData);
                }
            }
            catch (Exception ex)
            {
                _log.Error("Log out error", ex);
            }
        }

        public static string GetMD5Hash(string PlainText)
        {
            MD5 md = MD5.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(PlainText);
            byte[] buffer2 = md.ComputeHash(bytes);
            StringBuilder builder = new StringBuilder(buffer2.Length);
            for (int i = 0; i < buffer2.Length; i++)
            {
                builder.Append(buffer2[i].ToString("X2"));
            }
            return builder.ToString();
        }
    }
}
