﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using NLog;
using Sandbox.Game.World;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Session;
using Torch.Managers;
using Torch.Mod;
using VRage.Game;

namespace Torch.Session
{
    /// <summary>
    /// Manages the creation and destruction of <see cref="TorchSession"/> instances for each <see cref="MySession"/> created by Space Engineers.
    /// </summary>
    public class TorchSessionManager : Manager, ITorchSessionManager
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private TorchSession _currentSession;

        private readonly Dictionary<ulong, MyObjectBuilder_Checkpoint.ModItem> _overrideMods;

        public event Action<CollectionChangeEventArgs> OverrideModsChanged;

        /// <summary>
        /// List of mods that will be injected into client world downloads.
        /// </summary>
        public IReadOnlyCollection<MyObjectBuilder_Checkpoint.ModItem> OverrideMods => _overrideMods.Values;

        /// <inheritdoc />
        public event TorchSessionStateChangedDel SessionStateChanged;

        /// <inheritdoc/>
        public ITorchSession CurrentSession => _currentSession;

        private readonly HashSet<SessionManagerFactoryDel> _factories = new HashSet<SessionManagerFactoryDel>();

        public TorchSessionManager(ITorchBase torchInstance) : base(torchInstance)
        {
            _overrideMods = new Dictionary<ulong, MyObjectBuilder_Checkpoint.ModItem>();
            _overrideMods.Add(TorchModCore.MOD_ID, new MyObjectBuilder_Checkpoint.ModItem(TorchModCore.MOD_ID));
        }

        /// <inheritdoc/>
        public bool AddFactory(SessionManagerFactoryDel factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory), "Factory must be non-null");
            return _factories.Add(factory);
        }

        /// <inheritdoc/>
        public bool RemoveFactory(SessionManagerFactoryDel factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory), "Factory must be non-null");
            return _factories.Remove(factory);
        }

        /// <inheritdoc/>
        public bool AddOverrideMod(ulong modId)
        {
            if (_overrideMods.ContainsKey(modId))
                return false;
            var item = new MyObjectBuilder_Checkpoint.ModItem(modId);
            _overrideMods.Add(modId, item);

            OverrideModsChanged?.Invoke(new CollectionChangeEventArgs(CollectionChangeAction.Add, item));
            return true;
        }

        /// <inheritdoc/>
        public bool RemoveOverrideMod(ulong modId)
        {
            if(_overrideMods.TryGetValue(modId, out var item))
                OverrideModsChanged?.Invoke(new CollectionChangeEventArgs(CollectionChangeAction.Remove, item));

            return _overrideMods.Remove(modId);
        }

        #region Session events

        private void SetState(TorchSessionState state)
        {
            if (_currentSession == null)
                return;
            _currentSession.State = state;
            SessionStateChanged?.Invoke(_currentSession, _currentSession.State);
        }

        private void SessionLoading()
        {
            try
            {
                if (_currentSession != null)
                {
                    Log.Warn($"Override old torch session {_currentSession.KeenSession.Name}");
                    _currentSession.Detach();
                }

                Log.Info($"Starting new torch session for {MySession.Static.Name}");

                _currentSession = new TorchSession(Torch, MySession.Static);
                SetState(TorchSessionState.Loading);
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void SessionLoaded()
        {
            try
            {
                if (_currentSession == null)
                {
                    Log.Warn("Session loaded event occurred when we don't have a session.");
                    return;
                }
                foreach (SessionManagerFactoryDel factory in _factories)
                {
                    IManager manager = factory(CurrentSession);
                    if (manager != null)
                        CurrentSession.Managers.AddManager(manager);
                }
                (CurrentSession as TorchSession)?.Attach();
                Log.Info($"Loaded torch session for {MySession.Static.Name}");
                SetState(TorchSessionState.Loaded);
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void SessionUnloading()
        {
            try
            {
                if (_currentSession == null)
                {
                    Log.Warn("Session unloading event occurred when we don't have a session.");
                    return;
                }
                Log.Info($"Unloading torch session for {_currentSession.KeenSession.Name}");
                SetState(TorchSessionState.Unloading);
                _currentSession.Detach();
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void SessionUnloaded()
        {
            try
            {
                if (_currentSession == null)
                {
                    Log.Warn("Session unloading event occurred when we don't have a session.");
                    return;
                }
                Log.Info($"Unloaded torch session for {_currentSession.KeenSession.Name}");
                SetState(TorchSessionState.Unloaded);
                _currentSession = null;
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }
        #endregion

        /// <inheritdoc/>
        public override void Attach()
        {
            MySession.OnLoading += SessionLoading;
            MySession.AfterLoading += SessionLoaded;
            MySession.OnUnloading += SessionUnloading;
            MySession.OnUnloaded += SessionUnloaded;
        }


        /// <inheritdoc/>
        public override void Detach()
        {
            MySession.OnLoading -= SessionLoading;
            MySession.AfterLoading -= SessionLoaded;
            MySession.OnUnloading -= SessionUnloading;
            MySession.OnUnloaded -= SessionUnloaded;

            if (_currentSession != null)
            {
                if (_currentSession.State == TorchSessionState.Loaded)
                    SessionUnloading();
                if (_currentSession.State == TorchSessionState.Unloading)
                    SessionUnloaded();
            }
        }
    }
}
