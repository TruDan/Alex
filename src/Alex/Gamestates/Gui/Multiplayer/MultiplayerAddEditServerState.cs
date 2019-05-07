﻿using System;
using Alex.API.Data.Servers;
using Alex.API.Graphics;
using Alex.API.Graphics.Typography;
using Alex.API.Gui;
using Alex.API.Gui.Elements;
using Alex.API.Gui.Elements.Controls;
using Alex.API.Gui.Graphics;
using Alex.API.Services;
using Alex.API.Utils;
using Alex.GameStates.Gui.Common;
using Alex.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NLog;

namespace Alex.GameStates.Gui.Multiplayer
{
    public class MultiplayerAddEditServerState : GuiCallbackStateBase<SavedServerEntry>
    {
	    private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(MultiplayerConnectState));
        
        #region Gui Elements

        private readonly GuiTextInput _nameInput;
		private readonly GuiTextInput _hostnameInput;
	    private readonly GuiTextInput _portInput;
		private readonly GuiTextElement _errorMessage;
        private readonly GuiButton _saveButton;
	    private readonly GuiCustomButton _javaEditionButton;
	    private readonly GuiCustomButton _bedrockEditionButton;
	    private readonly GuiTextElement _serverTypeLabel;
		#endregion

		private readonly SavedServerEntry _entry;
        private readonly IListStorageProvider<SavedServerEntry> _savedServersStorage;
	    private readonly GuiPanoramaSkyBox _skyBox;
        public MultiplayerAddEditServerState(Action<SavedServerEntry> callbackAction, GuiPanoramaSkyBox skyBox) : this(null, null, callbackAction, skyBox)
        {

        }

        public MultiplayerAddEditServerState(string name, string address, Action<SavedServerEntry> callbackAction, GuiPanoramaSkyBox skyBox) :
            base(callbackAction)
        {
            _savedServersStorage = GetService<IListStorageProvider<SavedServerEntry>>();
	        _skyBox = skyBox;

			Title = "Add Server";

	        base.HeaderTitle.Anchor = Alignment.MiddleCenter;
	        base.HeaderTitle.FontStyle = FontStyle.Bold | FontStyle.DropShadow;
			Body.BackgroundOverlay = new Color(Color.Black, 0.5f);

			Body.ChildAnchor = Alignment.MiddleCenter;

	        var usernameRow = AddGuiRow(new GuiTextElement()
	        {
		        Text = "Server Name:",
		        Margin = new Thickness(0, 0, 5, 0)
	        }, _nameInput = new GuiTextInput()
	        {
		        TabIndex = 1,

		        Width = 200,

		        PlaceHolder = "Name of the server",
		        Margin = new Thickness(23, 5,5,5),
	        });
	        usernameRow.ChildAnchor = Alignment.MiddleCenter;
	        usernameRow.Orientation = Orientation.Horizontal;

	        var hostnameRow = AddGuiRow(new GuiTextElement()
	        {
		        Text = "Server Address:",
		        Margin = new Thickness(0, 0, 5, 0)
	        }, _hostnameInput = new GuiTextInput()
	        {
		        TabIndex = 1,

		        Width = 200,

		        PlaceHolder = "Hostname or IP",
		        Margin = new Thickness(5),
	        });
	        hostnameRow.ChildAnchor = Alignment.MiddleCenter;
	        hostnameRow.Orientation = Orientation.Horizontal;

	        var typeLabelRow = AddGuiRow(_serverTypeLabel = new GuiTextElement()
	        {
		        Text = "Server Type (Java):",
		        Margin = new Thickness(0, 0, 5, 0)
	        });
	        typeLabelRow.ChildAnchor = Alignment.MiddleCenter;
	        typeLabelRow.Orientation = Orientation.Horizontal;

			var typeStringRow = AddGuiRow(_javaEditionButton = new GuiCustomButton("Java", JavaEditionButtonPressed)
	        {
		        Margin = new Thickness(5),
		        Modern = true,
		        Width = 50,
				Enabled = false
			},
	        _bedrockEditionButton = new GuiCustomButton("Bedrock", BedrockButtonPressed)
	        {
		        Margin = new Thickness(5),
		        Modern = true,
		        Width = 50
	        });
	        typeStringRow.Orientation = Orientation.Horizontal;
	        typeStringRow.ChildAnchor = Alignment.MiddleCenter;

		  //	var portRow = AddGuiRow();
	      //  portRow.ChildAnchor = Alignment.MiddleCenter;

			var buttonRow = AddGuiRow(_saveButton = new GuiButton(OnSaveButtonPressed)
	        {
		        AccessKey = Keys.Enter,

		        TranslationKey = "addServer.add",
		        Margin = new Thickness(5),
		        Modern = false,
		        Width = 100
	        }, new GuiButton(OnCancelButtonPressed)
	        {
		        AccessKey = Keys.Escape,

		        TranslationKey = "gui.cancel",
		        Margin = new Thickness(5),
		        Modern = false,
		        Width = 100
	        });
	        buttonRow.ChildAnchor = Alignment.MiddleCenter;


            AddGuiElement(_errorMessage = new GuiTextElement()
            {
                TextColor = TextColor.Red
            });

	        if (!string.IsNullOrWhiteSpace(name))
	        {
		        _nameInput.Value = name;
	        }

	        if (!string.IsNullOrWhiteSpace(address))
	        {
		        _hostnameInput.Value = address;
	        }

	        if (_entry != null)
	        {
				EnableButtonsFor(_entry.ServerType);
	        }

			Background = new GuiTexture2D(_skyBox, TextureRepeatMode.Stretch);
		}

	    private void EnableButtonsFor(ServerType type)
	    {
		    if (type == ServerType.Bedrock)
		    {
			    _serverTypeLabel.Text = "Server Type (Bedrock):";

				_bedrockEditionButton.Enabled = false;
			    _javaEditionButton.Enabled = true;
			}
		    else
		    {
			    _serverTypeLabel.Text = "Server Type (Java):";

				_javaEditionButton.Enabled = false;
			    _bedrockEditionButton.Enabled = true;
		    }
		}

	    private void BedrockButtonPressed()
	    {
		    EnableButtonsFor(ServerType.Bedrock);
	    }

	    private void JavaEditionButtonPressed()
	    {
		    EnableButtonsFor(ServerType.Java);
		}

	    public MultiplayerAddEditServerState(SavedServerEntry entry, Action<SavedServerEntry> callbackAction, GuiPanoramaSkyBox skyBox) : this(entry.Name, entry.Host + ":" + entry.Port, callbackAction, skyBox)
        {
            if (entry != null)
            {
                _entry = entry;
            }
        }

        private void OnSaveButtonPressed()
        {
            try
            {
                var name = _nameInput.Value;
                var address = _hostnameInput.Value;

                ushort port = (ushort) (_javaEditionButton.Enabled ? 19132 : 25565);

                var split = address.Split(':', StringSplitOptions.RemoveEmptyEntries);
                var hostname = split[0];

                if (split.Length == 2)
                {
                    if (ushort.TryParse(split[1], out port))
                    {
                        SaveServer(name, hostname, port);
                    }
                    else
                    {
                        SetErrorMessage("Invalid Server Address!");
                    }
                }
                else if (split.Length == 1)
                {
                    SaveServer(name, hostname, port);
                }
                else
                {
                    SetErrorMessage("Invalid Server Address!");
                }
            }
            catch (Exception ex)
            {
				Log.Error($"Error: {ex.ToString()}");
                SetErrorMessage(ex.Message);
            }
        }

        private void OnCancelButtonPressed()
        {
            InvokeCallback(null);
        }

        private void SaveServer(string name, string hostname, ushort port)
        {
            var entry = new SavedServerEntry()
            {
                Name       = name,
                Host       = hostname,
                Port       = port,
                ServerType = _javaEditionButton.Enabled ? ServerType.Bedrock : ServerType.Java,
				CachedIcon = _entry?.CachedIcon ?? null,
				ListIndex = _entry?.ListIndex ?? -1
            };
            
            if (_entry != null)
            {
                _savedServersStorage.RemoveEntry(_entry);
            }
            _savedServersStorage.AddEntry(entry);

            InvokeCallback(entry);
        }
        
        private void SetErrorMessage(string error)
        {
            _errorMessage.Text = error;
        }

	    protected override void OnDraw(IRenderArgs args)
	    {
		    base.OnDraw(args);
		    _skyBox.Draw(args);
		}

	    protected override void OnUpdate(GameTime gameTime)
	    {
		    base.OnUpdate(gameTime);
		    _skyBox.Update(gameTime);
		}
    }
}
