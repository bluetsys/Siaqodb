using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Linq;
using Sqo;
using Sqo.Meta;
using Sqo.Queries;
using System.Linq.Expressions;
using Sqo.Exceptions;
using Sqo.Utilities;
using Sqo.Cache;
using System.Reflection;
using System.Threading;
using Sqo.Indexes;
#if WinRT
using Windows.Storage;
#endif
#if ASYNC
using System.Threading.Tasks;
#endif


namespace Sqo
{
    /// <summary>
    /// Main class of siaqodb database engine responsible for storing, retrieving ,deleting objects on database files
    /// </summary>
#if SILVERLIGHT || UNITY3D || TRIAL || CF || MONODROID || WinRT
#else

    [System.ComponentModel.LicenseProvider(typeof(SqoLicenseProvider))]
#endif
    [Obfuscation(Feature = "Apply to member * when event: all", Exclude = false,ApplyToMembers=true)]
    #if KEVAST
    internal
#else
        public
#endif
        class Siaqodb  
	{

        readonly object _syncRoot = new object();
#if ASYNC
        private readonly AsyncLock _lockerAsync = new AsyncLock();
#endif
        private readonly object _locker = new object();

        
#if WinRT
        StorageFolder databaseFolder;
#endif
        string path;
        StorageEngine storageEngine;
        Cache.CacheForManager cacheForManager;
        internal Cache.MetaCache metaCache;
        IndexManager indexManager;
        bool opened;
        internal List<object> circularRefCache = new List<object>();
        bool storeOnlyReferencesOfListItems;//used only in StoreObjectPartially to store only references of list items

        /// <summary>
        /// Raised before an object is saved in database
        /// </summary>
#if UNITY3D
        private EventHandler<SavingEventsArgs> savingObject;
        public event EventHandler<SavingEventsArgs> SavingObject
        {
            add
            {
                lock (_syncRoot)
                {
                    savingObject += value;
                }
            }
            remove
            {
                lock (_syncRoot)
                {
                    savingObject -= value;
                }
            }

        }
#else
        public event EventHandler<SavingEventsArgs> SavingObject;
#endif

        /// <summary>
        /// Raised after an object is saved in database
        /// </summary>
#if UNITY3D
        private EventHandler<SavedEventsArgs> savedObject;
        public event EventHandler<SavedEventsArgs> SavedObject
        {
            add
            {
                lock (_syncRoot)
                {
                    savedObject += value;
                }
            }
            remove
            {
                lock (_syncRoot)
                {
                    savedObject -= value;
                }
            }

        }
#else
        public event EventHandler<SavedEventsArgs> SavedObject;
#endif


        /// <summary>
        /// Raised before an object is deleted from database
        /// </summary>
#if UNITY3D
        private EventHandler<DeletingEventsArgs> deletingObject;
        public event EventHandler<DeletingEventsArgs> DeletingObject
        {
            add
            {
                lock (_syncRoot)
                {
                    deletingObject += value;
                }
            }
            remove
            {
                lock (_syncRoot)
                {
                    deletingObject -= value;
                }
            }

        }
#else
        		public event EventHandler<DeletingEventsArgs> DeletingObject;
#endif


        /// <summary>
        /// Raised after an object is deleted from database
        /// </summary>

#if UNITY3D
        private EventHandler<DeletedEventsArgs> deletedObject;
        public event EventHandler<DeletedEventsArgs> DeletedObject
        {
            add
            {
                lock (_syncRoot)
                {
                    deletedObject += value;
                }
            }
            remove
            {
                lock (_syncRoot)
                {
                    deletedObject -= value;
                }
            }

        }
#else
        	public event EventHandler<DeletedEventsArgs> DeletedObject;
#endif


        /// <summary>
        /// Raised before an object is loaded from database
        /// </summary>
#if UNITY3D
        private EventHandler<LoadingObjectEventArgs> loadingObject;
        public event EventHandler<LoadingObjectEventArgs> LoadingObject
        {
            add
            {
                lock (_syncRoot)
                {
                    loadingObject += value;
                }
            }
            remove
            {
                lock (_syncRoot)
                {
                    loadingObject -= value;
                }
            }

        }
#else
         public event EventHandler<LoadingObjectEventArgs> LoadingObject;
#endif


        /// <summary>
        /// Raised after object is loaded from database
        /// </summary>
#if UNITY3D
        private EventHandler<LoadedObjectEventArgs> loadedObject;
        public event EventHandler<LoadedObjectEventArgs> LoadedObject
        {
            add
            {
                lock (_syncRoot)
                {
                    loadedObject += value;
                }
            }
            remove
            {
                lock (_syncRoot)
                {
                    loadedObject -= value;
                }
            }

        }
#else
          public event EventHandler<LoadedObjectEventArgs> LoadedObject;
#endif

 #if UNITY3D || CF || MONODROID
#else
          public event EventHandler<IndexesSaveAsyncFinishedArgs> IndexesSaveAsyncFinished;
#endif
        /// <summary>
     /// Create a new instance of Siaqodb, database is not opened yet
     /// </summary>
        public Siaqodb()
        {
            this.CheckLic();
        }
        
        //TODO: add here WarningMessages and add for example Unoptimized queries
        /// <summary>
        /// Create a new instance of Siaqodb and open the database
        /// </summary>
        /// <param name="path">Physical folder name where objects are stored</param>
#if !WinRT
        public Siaqodb(string path)
        {
            this.CheckLic();
            this.Open(path);
        }
#endif
#if SL4
       /// <summary>
        ///Create a new instance of Siaqodb, open database for OOB mode
       /// </summary>
       /// <param name="folderName">database folder name</param>
       /// <param name="specialFolder">special folder name for OOB mode ex.:MyDocuments, MyPictures, etc</param>
        public Siaqodb(string folderName,Environment.SpecialFolder specialFolder)
        {
            this.CheckLic();
            this.Open(folderName,specialFolder);
        }
#endif

#if !WinRT
        internal Siaqodb(string path, bool cacheTypes)
        {
             this.CheckLic();
            
            this.opened = true;
            this.path = path;
            storageEngine = new StorageEngine(this.path);
            indexManager = new IndexManager(this);
            storageEngine.indexManager = indexManager;

            storageEngine.NeedSaveComplexObject += new EventHandler<Core.ComplexObjectEventArgs>(storageEngine_NeedSaveComplexObject);
#if ASYNC
            storageEngine.NeedSaveComplexObjectAsync += storageEngine_NeedSaveComplexObjectAsync;
#endif

            storageEngine.LoadingObject += new EventHandler<LoadingObjectEventArgs>(storageEngine_LoadingObject);
            storageEngine.LoadedObject += new EventHandler<LoadedObjectEventArgs>(storageEngine_LoadedObject);
#if UNITY3D || CF || MONODROID
#else

            storageEngine.IndexesSaveAsyncFinished += new EventHandler<IndexesSaveAsyncFinishedArgs>(storageEngine_IndexesSaveAsyncFinished);
#endif
            this.metaCache = new MetaCache();
            storageEngine.metaCache = this.metaCache;
            storageEngine.LoadMetaDataTypesForManager();
            cacheForManager = new Sqo.Cache.CacheForManager();
        }
#endif

        internal Siaqodb(string path, string managerOption)
        {
            this.CheckLic();
            this.opened = true;
            this.path = path;
            
#if  SILVERLIGHT
            storageEngine = new StorageEngine(this.path, true);
#else
            storageEngine = new StorageEngine(this.path);
#endif
            
            indexManager = new IndexManager(this);
            storageEngine.indexManager = indexManager;

            storageEngine.NeedSaveComplexObject += new EventHandler<Core.ComplexObjectEventArgs>(storageEngine_NeedSaveComplexObject);
#if ASYNC
            storageEngine.NeedSaveComplexObjectAsync += storageEngine_NeedSaveComplexObjectAsync;
#endif

            storageEngine.LoadedObject+=new EventHandler<LoadedObjectEventArgs>(storageEngine_LoadedObject);
            storageEngine.LoadingObject+=new EventHandler<LoadingObjectEventArgs>(storageEngine_LoadingObject);

            this.metaCache = new MetaCache();
            storageEngine.metaCache = this.metaCache;
            
            storageEngine.LoadAllTypes();
            List<SqoTypeInfo> typesForIndexes = this.metaCache.DumpAllTypes();
            this.indexManager.BuildAllIndexes(typesForIndexes);

            this.RecoverAfterCrash();
            cacheForManager = new Sqo.Cache.CacheForManager();
        }
#if !WinRT
        /// <summary>
        /// Open database folder
        /// </summary>
        /// <param name="path">path where objects are stored</param>
        public void Open(string path)
        {

            this.opened = true;
            this.path = path;
            this.metaCache = new MetaCache();
            storageEngine = new StorageEngine(this.path);
            indexManager = new IndexManager(this);
            storageEngine.indexManager = indexManager;

            storageEngine.metaCache = this.metaCache;
            storageEngine.NeedSaveComplexObject += new EventHandler<Core.ComplexObjectEventArgs>(storageEngine_NeedSaveComplexObject);
#if ASYNC
            storageEngine.NeedSaveComplexObjectAsync += storageEngine_NeedSaveComplexObjectAsync;
#endif
            storageEngine.LoadingObject+=new EventHandler<LoadingObjectEventArgs>(storageEngine_LoadingObject);
            storageEngine.LoadedObject+=new EventHandler<LoadedObjectEventArgs>(storageEngine_LoadedObject);
             #if UNITY3D || CF || MONODROID
#else
            storageEngine.IndexesSaveAsyncFinished += new EventHandler<IndexesSaveAsyncFinishedArgs>(storageEngine_IndexesSaveAsyncFinished);
#endif
            storageEngine.LoadAllTypes();
            List<SqoTypeInfo> typesForIndexes = this.metaCache.DumpAllTypes();
            this.indexManager.BuildAllIndexes(typesForIndexes);
            this.RecoverAfterCrash();
            cacheForManager = new Sqo.Cache.CacheForManager();

        }
        #if ASYNC
        public async Task OpenAsync(string path)
        {
            this.opened = true;
            this.path = path;
            this.metaCache = new MetaCache();
            storageEngine = new StorageEngine(this.path);
            indexManager = new IndexManager(this);
            storageEngine.indexManager = indexManager;

            storageEngine.metaCache = this.metaCache;
            storageEngine.NeedSaveComplexObject += new EventHandler<Core.ComplexObjectEventArgs>(storageEngine_NeedSaveComplexObject);
            storageEngine.NeedSaveComplexObjectAsync += storageEngine_NeedSaveComplexObjectAsync;
            storageEngine.LoadingObject += new EventHandler<LoadingObjectEventArgs>(storageEngine_LoadingObject);
            storageEngine.LoadedObject += new EventHandler<LoadedObjectEventArgs>(storageEngine_LoadedObject);
#if UNITY3D || CF || MONODROID
#else
            storageEngine.IndexesSaveAsyncFinished += new EventHandler<IndexesSaveAsyncFinishedArgs>(storageEngine_IndexesSaveAsyncFinished);
#endif
            await storageEngine.LoadAllTypesAsync().ConfigureAwait(false);
            List<SqoTypeInfo> typesForIndexes = this.metaCache.DumpAllTypes();
            await this.indexManager.BuildAllIndexesAsync(typesForIndexes);
            this.RecoverAfterCrash();
            cacheForManager = new Sqo.Cache.CacheForManager();
        }
       #endif
#endif
#if WinRT
        /// <summary>
        /// Open database folder
        /// </summary>
        /// <param name="databaseFolder">path where objects are stored</param>
        public async Task OpenAsync(StorageFolder databaseFolder)
        {

            this.opened = true;
            this.databaseFolder = databaseFolder;
            this.path = databaseFolder.Path;
            this.metaCache = new MetaCache();
            storageEngine = new StorageEngine(this.databaseFolder);
            indexManager = new IndexManager(this);
            storageEngine.indexManager = indexManager;
            storageEngine.metaCache = this.metaCache;
            storageEngine.NeedSaveComplexObject += new EventHandler<Core.ComplexObjectEventArgs>(storageEngine_NeedSaveComplexObject);
            storageEngine.NeedSaveComplexObjectAsync += storageEngine_NeedSaveComplexObjectAsync;
            storageEngine.LoadingObject += new EventHandler<LoadingObjectEventArgs>(storageEngine_LoadingObject);
            storageEngine.LoadedObject += new EventHandler<LoadedObjectEventArgs>(storageEngine_LoadedObject);

            await storageEngine.LoadAllTypesAsync();
            List<SqoTypeInfo> typesForIndexes = this.metaCache.DumpAllTypes();
            await this.indexManager.BuildAllIndexesAsync(typesForIndexes);

            await this.RecoverAfterCrashAsync();
            cacheForManager = new Sqo.Cache.CacheForManager();

            
        }
        /// <summary>
        /// Open database folder
        /// </summary>
        /// <param name="databaseFolder">path where objects are stored</param>
        public void Open(StorageFolder databaseFolder)
        {

            this.opened = true;
            this.databaseFolder = databaseFolder;
            this.path = databaseFolder.Path;
            this.metaCache = new MetaCache();
            storageEngine = new StorageEngine(this.databaseFolder);
            indexManager = new IndexManager(this);
            storageEngine.indexManager = indexManager;
            storageEngine.metaCache = this.metaCache;
            storageEngine.NeedSaveComplexObject += new EventHandler<Core.ComplexObjectEventArgs>(storageEngine_NeedSaveComplexObject);
            storageEngine.NeedSaveComplexObjectAsync += storageEngine_NeedSaveComplexObjectAsync;
            storageEngine.LoadingObject += new EventHandler<LoadingObjectEventArgs>(storageEngine_LoadingObject);
            storageEngine.LoadedObject += new EventHandler<LoadedObjectEventArgs>(storageEngine_LoadedObject);

            storageEngine.LoadAllTypes();
            List<SqoTypeInfo> typesForIndexes = this.metaCache.DumpAllTypes();
            this.indexManager.BuildAllIndexes(typesForIndexes);

            this.RecoverAfterCrash();
            cacheForManager = new Sqo.Cache.CacheForManager();


        }
#endif
#if SL4
        /// <summary>
        /// Open database
        /// </summary>
        /// <param name="folderName">the name of folder where datafiles will be saved</param>
        /// <param name="specialFolder">special folder for OOB mode,ex:MyDocuments,MyPictures etc</param>
        public void Open(string folderName, Environment.SpecialFolder specialFolder)
        {
            string specF = Environment.GetFolderPath(specialFolder);
            if (specF == null)
            {
                throw new SiaqodbException("Siaqodb can run in OOB mode only if specialFolder is set");
            }
            string path = specF + System.IO.Path.DirectorySeparatorChar + folderName;

            this.opened = true;
            this.path = path;
            this.metaCache = new MetaCache();
            storageEngine = new StorageEngine(this.path,true);
            indexManager = new IndexManager(this);
            storageEngine.indexManager = indexManager;
            storageEngine.metaCache = this.metaCache;
            storageEngine.NeedSaveComplexObject += new EventHandler<Core.ComplexObjectEventArgs>(storageEngine_NeedSaveComplexObject);
            storageEngine.LoadingObject += new EventHandler<LoadingObjectEventArgs>(storageEngine_LoadingObject);
            storageEngine.LoadedObject += new EventHandler<LoadedObjectEventArgs>(storageEngine_LoadedObject);

            storageEngine.LoadAllTypes();
            List<SqoTypeInfo> typesForIndexes = this.metaCache.DumpAllTypes();
            this.indexManager.BuildAllIndexes(typesForIndexes);
            this.RecoverAfterCrash();
            cacheForManager = new Sqo.Cache.CacheForManager();
        }

        
#endif

        #region EVENTs_HND

#if UNITY3D || CF || MONODROID
#else
        void storageEngine_IndexesSaveAsyncFinished(object sender, IndexesSaveAsyncFinishedArgs e)
        {
            this.OnIndexesSaveAsyncFinished(e);
        }
#endif
        void storageEngine_LoadedObject(object sender, LoadedObjectEventArgs e)
        {
            this.OnLoadedObject(e);
        }

        void storageEngine_LoadingObject(object sender, LoadingObjectEventArgs e)
        {
            this.OnLoadingObject(e);
        }

        void storageEngine_NeedSaveComplexObject(object sender, Core.ComplexObjectEventArgs e)
        {
            if (e.ComplexObject == null)
            {
                return;
            }
            SqoTypeInfo ti = this.GetSqoTypeInfoToStoreObject(e.ComplexObject);
            if (ti != null)
            {

                int oid = -1;
                if (e.ReturnOnlyOid_TID)
                {
                    oid = metaCache.GetOIDOfObject(e.ComplexObject, ti);
                }
                else if (circularRefCache.Contains(e.ComplexObject))
                {
                    oid = metaCache.GetOIDOfObject(e.ComplexObject, ti);
                }
                else
                {
                    circularRefCache.Add(e.ComplexObject);

                    oid = metaCache.GetOIDOfObject(e.ComplexObject, ti);
                    bool inserted = oid == 0;
                    if (this.storeOnlyReferencesOfListItems && !inserted)
                    {
                        //skip save object and keep only reference
                    }
                    else
                    {
                        oid = storageEngine.SaveObject(e.ComplexObject, ti);
                    }
                    SavedEventsArgs saved = new SavedEventsArgs(e.ComplexObject.GetType(), e.ComplexObject);
                    saved.Inserted = inserted;
                    this.OnSavedObject(saved);
                }
                e.SavedOID = oid;
                e.TID = ti.Header.TID;

            }
        }
#if ASYNC
        async Task storageEngine_NeedSaveComplexObjectAsync(object sender, Core.ComplexObjectEventArgs e)
        {
            if (e.ComplexObject == null)
            {
                return;
            }
            SqoTypeInfo ti = await this.GetSqoTypeInfoToStoreObjectAsync(e.ComplexObject);
            if (ti != null)
            {

                int oid = -1;
                if (e.ReturnOnlyOid_TID)
                {
                    oid = metaCache.GetOIDOfObject(e.ComplexObject, ti);
                }
                else if (circularRefCache.Contains(e.ComplexObject))
                {
                    oid = metaCache.GetOIDOfObject(e.ComplexObject, ti);
                }
                else
                {
                    circularRefCache.Add(e.ComplexObject);

                    oid = metaCache.GetOIDOfObject(e.ComplexObject, ti);
                    bool inserted = oid == 0;
                    if (this.storeOnlyReferencesOfListItems && !inserted)
                    {
                        //skip save object and keep only reference
                    }
                    else
                    {
                        oid = await storageEngine.SaveObjectAsync(e.ComplexObject, ti);
                    }
                    SavedEventsArgs saved = new SavedEventsArgs(e.ComplexObject.GetType(), e.ComplexObject);
                    saved.Inserted = inserted;
                    this.OnSavedObject(saved);
                }
                e.SavedOID = oid;
                e.TID = ti.Header.TID;

            }
        }
#endif
#if UNITY3D
        protected virtual void OnSavingObject(SavingEventsArgs e)
		{
			if (savingObject != null)
			{
				 if ((e.ObjectType.IsGenericType() && e.ObjectType.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>)) || e.ObjectType == typeof(Sqo.Indexes.IndexInfo2))
               {}
else
{
				savingObject(this, e);
}
			}
		}
		protected virtual void OnSavedObject(SavedEventsArgs e)
		{
			if (savedObject != null)
			{
 if ((e.ObjectType.IsGenericType() && e.ObjectType.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>)) || e.ObjectType == typeof(Sqo.Indexes.IndexInfo2))
               {}
else
{				
savedObject(this, e);
}
			}
		}
		protected virtual void OnDeletingObject(DeletingEventsArgs e)
		{
			if (deletingObject != null)
			{
				deletingObject(this, e);
			}
		}
		protected virtual void OnDeletedObject(DeletedEventsArgs e)
		{
			if (deletedObject != null)
			{
				deletedObject(this, e);
			}
		}
        protected virtual void OnLoadingObject(LoadingObjectEventArgs e)
        {
            if (loadingObject != null)
            {
                loadingObject(this, e);
            }
        }
        protected virtual void OnLoadedObject(LoadedObjectEventArgs e)
        {
            if (loadedObject != null)
            {
                loadedObject(this, e);
            }
        }
#else
        protected virtual void OnSavingObject(SavingEventsArgs e)
		{
			if (SavingObject != null)
			{
                if ((e.ObjectType.IsGenericType() && e.ObjectType.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>)) || e.ObjectType == typeof(Sqo.Indexes.IndexInfo2))
                { }
                else
                {
                    SavingObject(this, e);
                }
			}
		}
		protected virtual void OnSavedObject(SavedEventsArgs e)
		{
			if (SavedObject != null)
			{
                if ((e.ObjectType.IsGenericType() && e.ObjectType.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>)) || e.ObjectType == typeof(Sqo.Indexes.IndexInfo2))
                { }
                else
                {
                    SavedObject(this, e);
                }
			}
		}
		protected virtual void OnDeletingObject(DeletingEventsArgs e)
		{
			if (DeletingObject != null)
			{
				DeletingObject(this, e);
			}
		}
		protected virtual void OnDeletedObject(DeletedEventsArgs e)
		{
			if (DeletedObject != null)
			{
				DeletedObject(this, e);
			}
		}
        protected virtual void OnLoadingObject(LoadingObjectEventArgs e)
        {
            if (LoadingObject != null)
            {
                LoadingObject(this, e);
            }
        }
        protected virtual void OnLoadedObject(LoadedObjectEventArgs e)
        {
            if (LoadedObject != null)
            {
                LoadedObject(this, e);
            }
        }
        
#endif
        #if UNITY3D || CF || MONODROID
#else
        protected virtual void OnIndexesSaveAsyncFinished(IndexesSaveAsyncFinishedArgs e)
        {
            if (this.IndexesSaveAsyncFinished != null)
            {
                this.IndexesSaveAsyncFinished(this, e);
            }
        }
#endif

        #endregion
        
        /// <summary>
        /// Insert or update object; if object is loaded from database and this method is called then update will occur, if object is new created then insert will occur
        /// </summary>
        /// <param name="obj">Object to be stored</param>
		public void StoreObject(object obj)
		{
            lock (_locker)
            {
                SqoTypeInfo ti = this.GetSqoTypeInfoToStoreObject(obj);
                if (ti != null)
                {
                    if ((ti.Type.IsGenericType() && ti.Type.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>)) || ti.Type==typeof(IndexInfo2))
                    { }
                    else
                    {
                        circularRefCache.Clear();
                    }
                    circularRefCache.Add(obj);
                    bool inserted = false;
#if UNITY3D
                    if (this.savedObject != null)//optimization 
                    {
                        inserted = metaCache.GetOIDOfObject(obj, ti) == 0;
                    }
#else
                    if (this.SavedObject != null)//optimization 
                    {
                        inserted = metaCache.GetOIDOfObject(obj, ti) == 0;
                    }
#endif
                    storageEngine.SaveObject(obj, ti);
                    SavedEventsArgs saved = new SavedEventsArgs(obj.GetType(), obj);
                    saved.Inserted = inserted;
                    this.OnSavedObject(saved);


                }
            }

		}
#if ASYNC
        /// <summary>
        /// Insert or update object; if object is loaded from database and this method is called then update will occur, if object is new created then insert will occur
        /// </summary>
        /// <param name="obj">Object to be stored</param>
        public async Task StoreObjectAsync(object obj)
        {
            bool locked = false; await _lockerAsync.LockAsync(obj.GetType(), out locked);

            try
            {
                SqoTypeInfo ti = await this.GetSqoTypeInfoToStoreObjectAsync(obj);
                if (ti != null)
                {
                    if ((ti.Type.IsGenericType() && ti.Type.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>)) || ti.Type == typeof(IndexInfo2))
                    { }
                    else
                    {
                        circularRefCache.Clear();
                    }
                    circularRefCache.Add(obj);
                    bool inserted = false;
#if UNITY3D
                    if (this.savedObject != null)//optimization 
                    {
                        inserted = metaCache.GetOIDOfObject(obj, ti) == 0;
                    }
#else
                    if (this.SavedObject != null)//optimization 
                    {
                        inserted = metaCache.GetOIDOfObject(obj, ti) == 0;
                    }
#endif
                    await storageEngine.SaveObjectAsync(obj, ti);
                    SavedEventsArgs saved = new SavedEventsArgs(obj.GetType(), obj);
                    saved.Inserted = inserted;
                    this.OnSavedObject(saved);


                }
            }
            finally
            {

                if (locked) _lockerAsync.Release();

            }

        }
#endif
     /// <summary>
     ///  Insert or update object partially, only provided properties are saved
     /// </summary>
     /// <param name="obj">object of which properties will be stored</param>
     /// <param name="properties">properties to be stored</param>
        public void StoreObjectPartially(object obj,params string[] properties)
        {
            this.StoreObjectPartially(obj, false, properties);
        }
#if ASYNC
        /// <summary>
        ///  Insert or update object partially, only provided properties are saved
        /// </summary>
        /// <param name="obj">object of which properties will be stored</param>
        /// <param name="properties">properties to be stored</param>
        public async Task StoreObjectPartiallyAsync(object obj, params string[] properties)
        {
            await this.StoreObjectPartiallyAsync(obj, false, properties);
        }
#endif
        /// <summary>
        ///  Insert or update object partially, only provided properties are saved 
        /// </summary>
        /// <param name="obj">object of which properties will be stored</param>
        /// <param name="properties">properties to be stored</param>
        ///<param name="onlyReferences">if true,it will store only references to complex objects</param>
        public void StoreObjectPartially(object obj,bool onlyReferences, params string[] properties)
        {
            lock (_locker)
            {
                this.storeOnlyReferencesOfListItems = onlyReferences;
                SqoTypeInfo ti = this.GetSqoTypeInfoToStoreObject(obj);
                if (ti != null)
                {
                    circularRefCache.Clear();
                    circularRefCache.Add(obj);
                    storageEngine.SaveObjectPartially(obj, ti, properties);


                }
                this.storeOnlyReferencesOfListItems = false;
            }
        }
#if ASYNC
        /// <summary>
        ///  Insert or update object partially, only provided properties are saved 
        /// </summary>
        /// <param name="obj">object of which properties will be stored</param>
        /// <param name="properties">properties to be stored</param>
        ///<param name="onlyReferences">if true,it will store only references to complex objects</param>
        public async Task StoreObjectPartiallyAsync(object obj, bool onlyReferences, params string[] properties)
        {
             bool locked = false; await _lockerAsync.LockAsync(obj.GetType(), out locked);
             try
             {
                 this.storeOnlyReferencesOfListItems = onlyReferences;
                 SqoTypeInfo ti = await this.GetSqoTypeInfoToStoreObjectAsync(obj);
                 if (ti != null)
                 {
                     circularRefCache.Clear();
                     circularRefCache.Add(obj);
                     await storageEngine.SaveObjectPartiallyAsync(obj, ti, properties);

                 }
                 this.storeOnlyReferencesOfListItems = false;
             }
             finally
             {
                 if (locked) _lockerAsync.Release();
             }
        }
#endif
        /// <summary>
        /// Insert or update object by a Transaction; if object is loaded from database and this method is called then update will occur, if object is new created then insert will occur
        /// </summary>
        /// <param name="obj">Object to be stored</param>
        /// <param name="transaction">Transaction object</param>
		
        public void StoreObject(object obj,Transactions.Transaction transaction)
        {
            lock (_locker)
            {
                if (transaction == null)
                {
                    throw new ArgumentNullException("transaction");
                }
                 SqoTypeInfo ti = this.GetSqoTypeInfoToStoreObject(obj);
                if (ti != null)
                {
                    if (transaction.status == Transactions.TransactionStatus.Closed)
                    {
                        throw new SiaqodbException("Transaction closed!");
                    }
                    //circularRefCache.Clear();
                    //circularRefCache.Add(obj); 

                    //circularRefCache is filled with obj just before Commit in TransactionManager, so not need to be added here
                    storageEngine.SaveObject(obj, ti, null, transaction);

                    SavedEventsArgs saved = new SavedEventsArgs(obj.GetType(), obj);
                    this.OnSavedObject(saved);
                }
            }
        }
#if ASYNC
        /// <summary>
        /// Insert or update object by a Transaction; if object is loaded from database and this method is called then update will occur, if object is new created then insert will occur
        /// </summary>
        /// <param name="obj">Object to be stored</param>
        /// <param name="transaction">Transaction object</param>

        public async Task StoreObjectAsync(object obj, Transactions.Transaction transaction)
        {

            if (transaction == null)
            {
                throw new ArgumentNullException("transaction");
            }
            bool locked = false; await _lockerAsync.LockAsync(obj.GetType(), out locked);
            try
            {
                SqoTypeInfo ti = await this.GetSqoTypeInfoToStoreObjectAsync(obj);
                if (ti != null)
                {
                    if (transaction.status == Transactions.TransactionStatus.Closed)
                    {
                        throw new SiaqodbException("Transaction closed!");
                    }
                    //circularRefCache.Clear();
                    //circularRefCache.Add(obj); 

                    //circularRefCache is filled with obj just before Commit in TransactionManager, so not need to be added here
                    await storageEngine.SaveObjectAsync(obj, ti, null, transaction);

                    SavedEventsArgs saved = new SavedEventsArgs(obj.GetType(), obj);
                    this.OnSavedObject(saved);
                }
            }
            finally
            {
                if (locked) _lockerAsync.Release();
            }
        }
#endif
        
        private SqoTypeInfo GetSqoTypeInfoToStoreObject(object obj)
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            Type objType = obj.GetType();
            SavingEventsArgs ev = new SavingEventsArgs(objType, obj);

            this.OnSavingObject(ev);
            if (ev.Cancel)
            {
                return null;
            }

            return this.GetSqoTypeInfoToStoreObject(obj.GetType());
        }
#if ASYNC
        private async Task<SqoTypeInfo> GetSqoTypeInfoToStoreObjectAsync(object obj)
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            Type objType = obj.GetType();
            SavingEventsArgs ev = new SavingEventsArgs(objType, obj);

            this.OnSavingObject(ev);
            if (ev.Cancel)
            {
                return null;
            }

            return await this.GetSqoTypeInfoToStoreObjectAsync(obj.GetType());
        }
#endif
        private SqoTypeInfo GetSqoTypeInfoToStoreObject(Type objType)
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            SqoTypeInfo ti = null;
           

            if (this.metaCache.Contains(objType))
            {
                ti = this.metaCache.GetSqoTypeInfo(objType);
            }
            else
            {
                ti = MetaExtractor.GetSqoTypeInfo(objType);
                storageEngine.SaveType(ti);
                this.metaCache.AddType(objType, ti);
                this.indexManager.BuildIndexes(ti);

            }
            if (ti.IsOld)
            {
                throw new TypeChangedException("Actual runtime Type:" + ti.Type.Name + "is different than Type stored in DB, in current version is not supported automatically type changing, to fix this, modify your class like it was when u saved objects in DB");
            }
            return ti;
        }
#if ASYNC
        private async Task<SqoTypeInfo> GetSqoTypeInfoToStoreObjectAsync(Type objType)
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            SqoTypeInfo ti = null;


            if (this.metaCache.Contains(objType))
            {
                ti = this.metaCache.GetSqoTypeInfo(objType);
            }
            else
            {
                ti = MetaExtractor.GetSqoTypeInfo(objType);
                await storageEngine.SaveTypeAsync(ti);
                this.metaCache.AddType(objType, ti);
                await this.indexManager.BuildIndexesAsync(ti);

            }
            if (ti.IsOld)
            {
                throw new TypeChangedException("Actual runtime Type:" + ti.Type.Name + "is different than Type stored in DB, in current version is not supported automatically type changing, to fix this, modify your class like it was when u saved objects in DB");
            }
            return ti;
        }
#endif
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        internal IObjectList<T> Load<T>(System.Linq.Expressions.Expression expression)
        {
            lock (_locker)
            {
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();

                List<int> oids = LoadOids<T>(expression);
                return storageEngine.LoadByOIDs<T>(oids, ti);
            }

        }
#if ASYNC
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        internal async Task<IObjectList<T>> LoadAsync<T>(System.Linq.Expressions.Expression expression)
        {
            bool locked = false; await _lockerAsync.LockAsync(typeof(T), out locked);
            SqoTypeInfo ti = null;
            try
            {
                ti = CheckDBAndGetSqoTypeInfo<T>();
            }
            finally
            {
                if (locked) _lockerAsync.Release();
            }
            List<int> oids = await LoadOidsAsync<T>(expression);

            locked = false; await _lockerAsync.LockAsync(typeof(T), out locked);
            try
            {
                return await storageEngine.LoadByOIDsAsync<T>(oids, ti);
            }
            finally
            {
                if (locked) _lockerAsync.Release();
            }
        }
#endif
        //public IObjectList<T> Objects<T>()
        //{
        //    return this.LoadAll<T>();
        //}
        /// <summary>
        /// Load all objects of Type provided
        /// </summary>
        /// <typeparam name="T">Type of objects to be loaded from database</typeparam>
        /// <returns>List of objects retrieved from database</returns>
        public IObjectList<T> LoadAll<T>()
        {
            lock (_locker)
            {
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                return storageEngine.LoadAll<T>(ti);
            }
        }
#if ASYNC
        /// <summary>
        /// Load all objects of Type provided
        /// </summary>
        /// <typeparam name="T">Type of objects to be loaded from database</typeparam>
        /// <returns>List of objects retrieved from database</returns>
        public async Task<IObjectList<T>> LoadAllAsync<T>()
        {
             bool locked = false; await _lockerAsync.LockAsync(typeof(T), out locked);
             try
             {
                 SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                 return await storageEngine.LoadAllAsync<T>(ti);
             }
             finally
             {
                 if (locked) _lockerAsync.Release();
             }
        }
#endif
        /// <summary>
        /// Load object from database by OID provided
        /// </summary>
        /// <typeparam name="T">The Type of object to be loaded</typeparam>
        /// <param name="oid">oid of object</param>
        /// <returns>the object stored in database with oid provided</returns>
        public T LoadObjectByOID<T>(int oid)
        {
            lock (_locker)
            {
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                return storageEngine.LoadObjectByOID<T>(ti, oid);
            }
        }
#if ASYNC
        /// <summary>
        /// Load object from database by OID provided
        /// </summary>
        /// <typeparam name="T">The Type of object to be loaded</typeparam>
        /// <param name="oid">oid of object</param>
        /// <returns>the object stored in database with oid provided</returns>
        public async Task<T> LoadObjectByOIDAsync<T>(int oid)
        {
             bool locked = false; await _lockerAsync.LockAsync(typeof(T), out locked);
             try
             {
                 SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                 return await storageEngine.LoadObjectByOIDAsync<T>(ti, oid);
             }
             finally
             {
                 if (locked) _lockerAsync.Release();
             }

        }
#endif
        internal T LoadObjectByOID<T>(int oid,List<string> properties)
        {
            lock (_locker)
            {
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                return (T)storageEngine.LoadObjectByOID(ti, oid, properties);
            }
        }
#if ASYNC
        internal async Task<T> LoadObjectByOIDAsync<T>(int oid, List<string> properties)
        {
            bool locked;
            await _lockerAsync.LockAsync(typeof(T),out locked);
            try
            {
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                return (T)(await storageEngine.LoadObjectByOIDAsync(ti, oid, properties));
            }
            finally
            {
                if (locked) _lockerAsync.Release();
            }

        }
#endif
        /// <summary>
        /// Close database
        /// </summary>
        public void Close()
        {
            lock (_locker)
            {
                this.opened = false;
                this.metaCache = null;
                this.storageEngine.Close();
                this.indexManager.Close();
            }
        }
#if ASYNC
        /// <summary>
        /// Close database
        /// </summary>
        public async Task CloseAsync()
        {

            this.opened = false;
            this.metaCache = null;
            await this.storageEngine.CloseAsync();
            this.indexManager.Close();

        }
#endif
        /// <summary>
        /// Flush buffered data to database
        /// </summary>
		public void Flush()
		{
            lock (_locker)
            {
                this.storageEngine.Flush();
            }
		}
#if ASYNC
        /// <summary>
        /// Flush buffered data to database
        /// </summary>
        public async Task FlushAsync()
        {
            await _lockerAsync.LockAsync();
            try
            {
                await this.storageEngine.FlushAsync();
            }
            finally
            {
                _lockerAsync.Release();
            }
        }
#endif
        /// <summary>
        /// Cast method to be used in LINQ queries
        /// </summary>
        /// <typeparam name="T">Type over which LINQ will take action</typeparam>
        /// <returns></returns>
        public  ISqoQuery<T> Cast<T>()
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
           return new SqoQuery<T>(this);
        }
        /// <summary>
        /// Query method to be used in LINQ queries
        /// </summary>
        /// <typeparam name="T">Type over which LINQ will take action</typeparam>
        /// <returns></returns>
        public ISqoQuery<T> Query<T>()
        {
            return this.Cast<T>();
        }
        /// <summary>
        /// Load OIDs by expression
        /// </summary>
        /// <typeparam name="T">Type for which OIDs will be loaded</typeparam>
        /// <param name="expression">filter expression</param>
        /// <returns>List of OIDs</returns>
        public List<int> LoadOids<T>(System.Linq.Expressions.Expression expression)
        {
            lock (_locker)
            {
                if (expression == null)
                {
                    throw new ArgumentNullException("expression");
                }
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                QueryTranslator t = new QueryTranslator(this.storageEngine, ti);
                ICriteria criteria = t.Translate(expression);
                return criteria.GetOIDs();
            }
        }
#if ASYNC
        /// <summary>
        /// Load OIDs by expression
        /// </summary>
        /// <typeparam name="T">Type for which OIDs will be loaded</typeparam>
        /// <param name="expression">filter expression</param>
        /// <returns>List of OIDs</returns>
        public async Task<List<int>> LoadOidsAsync<T>(System.Linq.Expressions.Expression expression)
        {
            bool locked = false; await _lockerAsync.LockAsync(typeof(T), out locked);
            try
            {
                if (expression == null)
                {
                    throw new ArgumentNullException("expression");
                }
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                QueryTranslator t = new QueryTranslator(this.storageEngine, ti);
                ICriteria criteria = t.Translate(expression);
                return await criteria.GetOIDsAsync();
            }
            finally
            {
                if (locked) _lockerAsync.Release();
            }
        }
#endif
		internal List<int> LoadAllOIDs<T>()
		{
            lock (_locker)
            {
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                return storageEngine.LoadAllOIDs(ti);
            }
		}
#if ASYNC
        internal async Task<List<int>> LoadAllOIDsAsync<T>()
        {
            bool locked = false; await _lockerAsync.LockAsync(typeof(T), out locked);
            try
            {
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                return await storageEngine.LoadAllOIDsAsync(ti);
            }
            finally
            {
                if (locked) _lockerAsync.Release();
            }
        }
#endif

		/// <summary>
		/// Load all object OIDs of MetaType provided
		/// </summary>
		/// <param name="type">meta type Load by method GetAllTypes()</param>
		/// <returns></returns>
		public List<int> LoadAllOIDs(MetaType type)
		{
            lock (_locker)
            {
                if (!opened)
                {
                    throw new SiaqodbException("Database is closed, call method Open() to open it!");
                }
                return storageEngine.LoadAllOIDs(type.Name);
            }
		}
#if ASYNC
        /// <summary>
        /// Load all object OIDs of MetaType provided
        /// </summary>
        /// <param name="type">meta type Load by method GetAllTypes()</param>
        /// <returns></returns>
        public async Task<List<int>> LoadAllOIDsAsync(MetaType type)
        {

            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            return await storageEngine.LoadAllOIDsAsync(type.Name);
        }
#endif
        internal SqoTypeInfo GetSqoTypeInfo<T>()
        {
           
            Type objType = typeof(T);
            return this.GetSqoTypeInfo(objType);
        }

		internal SqoTypeInfo GetSqoTypeInfo(Type t)
		{
			SqoTypeInfo ti = null;
			Type objType =t;
			if (this.metaCache.Contains(objType))
			{
                ti = metaCache.GetSqoTypeInfo(objType);
			}
			else
			{
				ti = MetaExtractor.GetSqoTypeInfo(objType);
			}
			return ti;
		}


        internal List<KeyValuePair<int, int>> LoadOidsForJoin<TResult, TOuter, TInner>(SqoQuery<TOuter> outer, SqoQuery<TInner> inner, System.Linq.Expressions.Expression outerExpression, System.Linq.Expressions.Expression innerExpression)
        {

            SqoTypeInfo tiOuter = this.GetSqoTypeInfo<TOuter>();
            SqoTypeInfo tiInner = this.GetSqoTypeInfo<TInner>();

            JoinTranslator t = new JoinTranslator();
            string criteriaOuter = t.Translate(outerExpression);

            string criteriaInner = t.Translate(innerExpression);
            List<int> oidOuter = outer.GetFilteredOids();
            List<int> oidInner = inner.GetFilteredOids();

            List<KeyValuePair<int, int>> oidsPairs = storageEngine.LoadJoin(tiOuter, criteriaOuter, oidOuter, tiInner, criteriaInner, oidInner);

            return oidsPairs;


        }
#if ASYNC
        internal async Task<List<KeyValuePair<int, int>>> LoadOidsForJoinAsync<TResult, TOuter, TInner>(SqoQuery<TOuter> outer, SqoQuery<TInner> inner, System.Linq.Expressions.Expression outerExpression, System.Linq.Expressions.Expression innerExpression)
        {

            SqoTypeInfo tiOuter = this.GetSqoTypeInfo<TOuter>();
            SqoTypeInfo tiInner = this.GetSqoTypeInfo<TInner>();

            JoinTranslator t = new JoinTranslator();
            string criteriaOuter = t.Translate(outerExpression);

            string criteriaInner = t.Translate(innerExpression);
            List<int> oidOuter = outer.GetFilteredOids();
            List<int> oidInner = inner.GetFilteredOids();

            List<KeyValuePair<int, int>> oidsPairs = await storageEngine.LoadJoinAsync(tiOuter, criteriaOuter, oidOuter, tiInner, criteriaInner, oidInner);

            return oidsPairs;


        }

#endif


        internal object LoadValue(int oid, string fieldName, Type type)
        {

            SqoTypeInfo ti = this.GetSqoTypeInfo(type);
            return storageEngine.LoadValue(oid, fieldName, ti);

        }
#if ASYNC
        internal async Task<object> LoadValueAsync(int oid, string fieldName, Type type)
        {

            SqoTypeInfo ti = this.GetSqoTypeInfo(type);
            return await storageEngine.LoadValueAsync(oid, fieldName, ti);

        }
#endif
		/// <summary>
		/// Load value of a field of an object identified by OID provided
		/// </summary>
		/// <param name="oid">OID of object</param>
		/// <param name="fieldName">fieldName</param>
		/// <param name="mt">MetaType</param>
		/// <returns></returns>
		public object LoadValue(int oid, string fieldName, MetaType mt)
		{
            lock (_locker)
            {
                if (!opened)
                {
                    throw new SiaqodbException("Database is closed, call method Open() to open it!");
                }
                if (!cacheForManager.Contains(mt.Name))
                {
                    SqoTypeInfo ti = storageEngine.GetSqoTypeInfo(mt.Name);
                    cacheForManager.AddType(mt.Name, ti);
                }
                SqoTypeInfo tinf = cacheForManager.GetSqoTypeInfo(mt.Name);
                return storageEngine.LoadValue(oid, fieldName, tinf);
            }
		}
#if ASYNC
        /// <summary>
        /// Load value of a field of an object identified by OID provided
        /// </summary>
        /// <param name="oid">OID of object</param>
        /// <param name="fieldName">fieldName</param>
        /// <param name="mt">MetaType</param>
        /// <returns></returns>
        public async Task<object> LoadValueAsync(int oid, string fieldName, MetaType mt)
        {

            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            if (!cacheForManager.Contains(mt.Name))
            {
                SqoTypeInfo ti = await storageEngine.GetSqoTypeInfoAsync(mt.Name);
                cacheForManager.AddType(mt.Name, ti);
            }
            SqoTypeInfo tinf = cacheForManager.GetSqoTypeInfo(mt.Name);
            bool locked = false;
            await _lockerAsync.LockAsync(tinf.Type,out locked);
            try
            {
                return await storageEngine.LoadValueAsync(oid, fieldName, tinf);
            }
            finally { if (locked) _lockerAsync.Release(); }
        }
#endif
        /// <summary>
        /// Delete an object from database
        /// </summary>
        /// <param name="obj">Object to be deleted</param>
		public void Delete(object obj)
		{
            lock (_locker)
            {
                if (!opened)
                {
                    throw new SiaqodbException("Database is closed, call method Open() to open it!");
                }
                Type t = obj.GetType();
                SqoTypeInfo ti = this.GetSqoTypeInfo(t);
                bool deleted = DeleteObjInternal(obj, ti, null);
            }
		}
#if ASYNC
        /// <summary>
        /// Delete an object from database
        /// </summary>
        /// <param name="obj">Object to be deleted</param>
        public async Task DeleteAsync(object obj)
        {
            bool locked = false; await _lockerAsync.LockAsync(obj.GetType(), out locked);
            try
            {
                if (!opened)
                {
                    throw new SiaqodbException("Database is closed, call method Open() to open it!");
                }
                Type t = obj.GetType();
                SqoTypeInfo ti = this.GetSqoTypeInfo(t);
                bool deleted = await DeleteObjInternalAsync(obj, ti, null);
            }
            finally { if (locked) _lockerAsync.Release(); }
        }
#endif
        /// <summary>
        /// Delete an object from database using a Transaction
        /// </summary>
        /// <param name="obj">Object to be deleted</param>
        /// <param name="transaction">Transaction</param>
        public void Delete(object obj, Transactions.Transaction transaction)
        {
            lock (_locker)
            {
                if (!opened)
                {
                    throw new SiaqodbException("Database is closed, call method Open() to open it!");
                }

                if (transaction == null)
                {
                    throw new ArgumentNullException("transaction");
                }
               
                if (transaction.status == Transactions.TransactionStatus.Closed)
                {
                    throw new SiaqodbException("Transaction closed!");
                }
                Type t = obj.GetType();
                SqoTypeInfo ti = this.GetSqoTypeInfo(t);
                DeleteObjInternal(obj, ti, transaction);
            }
        }
#if ASYNC
        /// <summary>
        /// Delete an object from database using a Transaction
        /// </summary>
        /// <param name="obj">Object to be deleted</param>
        /// <param name="transaction">Transaction</param>
        public async Task DeleteAsync(object obj, Transactions.Transaction transaction)
        {
            bool locked = false; await _lockerAsync.LockAsync(obj.GetType(), out locked);
            try
            {
                if (!opened)
                {
                    throw new SiaqodbException("Database is closed, call method Open() to open it!");
                }
                if (transaction == null)
                {
                    throw new ArgumentNullException("transaction");
                }
                if (transaction.status == Transactions.TransactionStatus.Closed)
                {
                    throw new SiaqodbException("Transaction closed!");
                }
                Type t = obj.GetType();
                SqoTypeInfo ti = this.GetSqoTypeInfo(t);
                await DeleteObjInternalAsync(obj, ti, transaction);

            }
            finally { if (locked) _lockerAsync.Release(); }
        }
#endif
        /// <summary>
        /// Delete an object from database by a certain field(ex:ID that come from server)
        /// </summary>
        /// <param name="obj">Object to be deleted</param>
        /// <param name="fieldName">Names of field that this method will lookup for object to delete it</param>
        public bool DeleteObjectBy(string fieldName,object obj)
        {
            return this.DeleteObjectBy(obj, fieldName);
        }
#if ASYNC
        /// <summary>
        /// Delete an object from database by a certain field(ex:ID that come from server)
        /// </summary>
        /// <param name="obj">Object to be deleted</param>
        /// <param name="fieldName">Names of field that this method will lookup for object to delete it</param>
        public async Task<bool> DeleteObjectByAsync(string fieldName, object obj)
        {
            return await this.DeleteObjectByAsync(obj, fieldName);
        }
#endif
        /// <summary>
        /// Delete an object from database by a certain field(ex:ID that come from server)
        /// </summary>
        /// <param name="obj">Object to be deleted</param>
        /// <param name="fieldNames">Names of fields that this method will lookup for object to delete it</param>
        public bool DeleteObjectBy(object obj,params string[] fieldNames)
        {
            return this.DeleteObjectBy(obj, null, fieldNames);
        }
#if ASYNC
        /// <summary>
        /// Delete an object from database by a certain field(ex:ID that come from server)
        /// </summary>
        /// <param name="obj">Object to be deleted</param>
        /// <param name="fieldNames">Names of fields that this method will lookup for object to delete it</param>
        public async Task<bool> DeleteObjectByAsync(object obj, params string[] fieldNames)
        {
            return await this.DeleteObjectByAsync(obj, null, fieldNames);
        }
#endif
        /// <summary>
        /// Delete an object from database by a certain field(ex:ID that come from server)
        /// </summary>
        /// <param name="obj">Object to be deleted</param>
        /// <param name="fieldNames">Names of fields that this method will lookup for object to delete it</param>
        /// <param name="transaction">Transaction object</param>

        public bool DeleteObjectBy(object obj, Transactions.Transaction transaction, params string[] fieldNames)
        {
            lock (_locker)
            {
                if (fieldNames == null || fieldNames.Length == 0)
                {
                    throw new ArgumentNullException("fieldNames");
                }
                if (!opened)
                {
                    throw new SiaqodbException("Database is closed, call method Open() to open it!");
                }


                if (transaction != null)
                {
                    if (transaction.status == Transactions.TransactionStatus.Closed)
                    {
                        throw new SiaqodbException("Transaction closed!");
                    }
                }
               
                Type t = obj.GetType();
                SqoTypeInfo ti = this.GetSqoTypeInfo(t);
                DeletingEventsArgs delEv = new DeletingEventsArgs(ti.Type, -1);//we don't know it
                this.OnDeletingObject(delEv);
                if (delEv.Cancel)
                {
                    return false;
                }

                int OID_deleted = storageEngine.DeleteObjectBy(fieldNames, obj, ti, transaction);
                DeletedEventsArgs deletedEv = new DeletedEventsArgs(ti.Type, OID_deleted);
                this.OnDeletedObject(deletedEv);

                return OID_deleted > 0;
            }
        }
#if ASYNC
        /// <summary>
        /// Delete an object from database by a certain field(ex:ID that come from server)
        /// </summary>
        /// <param name="obj">Object to be deleted</param>
        /// <param name="fieldNames">Names of fields that this method will lookup for object to delete it</param>
        /// <param name="transaction">Transaction object</param>

        public async Task<bool> DeleteObjectByAsync(object obj, Transactions.Transaction transaction, params string[] fieldNames)
        {


            if (fieldNames == null || fieldNames.Length == 0)
            {
                throw new ArgumentNullException("fieldNames");
            }
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            bool locked = false; await _lockerAsync.LockAsync(obj.GetType(), out locked);
            try
            {
                if (transaction != null)
                {
                    if (transaction.status == Transactions.TransactionStatus.Closed)
                    {
                        throw new SiaqodbException("Transaction closed!");
                    }
                }
                Type t = obj.GetType();
                SqoTypeInfo ti = this.GetSqoTypeInfo(t);
                DeletingEventsArgs delEv = new DeletingEventsArgs(ti.Type, -1);//we don't know it
                this.OnDeletingObject(delEv);
                if (delEv.Cancel)
                {
                    return false;
                }

                int OID_deleted = await storageEngine.DeleteObjectByAsync(fieldNames, obj, ti, transaction);
                DeletedEventsArgs deletedEv = new DeletedEventsArgs(ti.Type, OID_deleted);
                this.OnDeletedObject(deletedEv);

                return OID_deleted>0;
            }
            finally
            {
                if (locked) _lockerAsync.Release();
            }
        }
#endif
         /// <summary>
        /// Delete an object from database by a criteria
        /// </summary>
        /// <param name="criteria">Pairs of fields-values to lookup for object to delete it</param>
        /// <returns>Number of objects deleted</returns>
        public int DeleteObjectBy(Type objectType,Dictionary<string, object> criteria)
        {
            lock (_locker)
            {
                if (criteria == null || criteria.Keys.Count == 0)
                {
                    throw new ArgumentNullException("criteria");
                }
                if (!opened)
                {
                    throw new SiaqodbException("Database is closed, call method Open() to open it!");
                }

                SqoTypeInfo ti = this.GetSqoTypeInfo(objectType);
                DeletingEventsArgs delEv = new DeletingEventsArgs(ti.Type, -1);//we don't know it
                this.OnDeletingObject(delEv);
                if (delEv.Cancel)
                {
                    return 0;
                }

                List<int> oidsDeleted = storageEngine.DeleteObjectBy(ti, criteria);
                foreach (int oid in oidsDeleted)
                {
                    DeletedEventsArgs deletedEv = new DeletedEventsArgs(ti.Type, oid);
                    this.OnDeletedObject(deletedEv);
                }
                return oidsDeleted.Count;
            }
        }
        /// <summary>
        /// Delete an object from database by a criteria
        /// </summary>
        /// <param name="criteria">Pairs of fields-values to lookup for object to delete it</param>
        /// <returns>Number of objects deleted</returns>
        public int DeleteObjectBy<T>(Dictionary<string,object> criteria)
        {
            return DeleteObjectBy(typeof(T), criteria);
        }
#if ASYNC
        /// <summary>
        /// Delete an object from database by a criteria
        /// </summary>
        /// <param name="criteria">Pairs of fields-values to lookup for object to delete it</param>
        /// <returns>Number of objects deleted</returns>

        public async Task<int> DeleteObjectByAsync(Type objectType,Dictionary<string, object> criteria)
        {
            if (criteria == null || criteria.Keys.Count == 0)
            {
                throw new ArgumentNullException("criteria");
            }
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            bool locked = false; await _lockerAsync.LockAsync(objectType, out locked);
            try
            {
                SqoTypeInfo ti = this.GetSqoTypeInfo(objectType);
                DeletingEventsArgs delEv = new DeletingEventsArgs(ti.Type, -1);//we don't know it
                this.OnDeletingObject(delEv);
                if (delEv.Cancel)
                {
                    return 0;
                }

                List<int> oidsDeleted = await storageEngine.DeleteObjectByAsync(ti, criteria);
                foreach (int oid in oidsDeleted)
                {
                    DeletedEventsArgs deletedEv = new DeletedEventsArgs(ti.Type, oid);
                    this.OnDeletedObject(deletedEv);
                }
                return oidsDeleted.Count;

            }
            finally
            {
                if (locked) _lockerAsync.Release();
            }
        }

        /// <summary>
        /// Delete an object from database by a criteria
        /// </summary>
        /// <param name="criteria">Pairs of fields-values to lookup for object to delete it</param>
        /// <returns>Number of objects deleted</returns>
        public async Task<int> DeleteObjectByAsync<T>(Dictionary<string, object> criteria)
        {
            return await DeleteObjectByAsync(typeof(T), criteria);
            
        }
#endif
        
        /// <summary>
        /// Delete all objects of Type provided
        /// </summary>
        /// <typeparam name="T">Type of objects to be deleted</typeparam>
		public void DropType<T>()
		{
            this.DropType(typeof(T));
		}
#if ASYNC
        /// <summary>
        /// Delete all objects of Type provided
        /// </summary>
        /// <typeparam name="T">Type of objects to be deleted</typeparam>
        public async Task DropTypeAsync<T>()
        {
            await this.DropTypeAsync(typeof(T));
        }
#endif
        /// <summary>
        /// Delete all objects of Type provided
        /// </summary>
        /// <param name="type">Type of objects to be deleted</param>>
        public void DropType(Type type)
        {
            this.DropType(type, false);
        }
#if ASYNC
        /// <summary>
        /// Delete all objects of Type provided
        /// </summary>
        /// <param name="type">Type of objects to be deleted</param>>
        public async Task DropTypeAsync(Type type)
        {
            await this.DropTypeAsync(type, false);
        }
#endif
        /// <summary>
        ///  Delete all objects of Type provided
        /// </summary>
        /// <param name="type">Type of objects to be deleted</param>
        /// <param name="claimFreespace">If this is TRUE all dynamic length data associated with objects will be marked as free and Shrink method is able to free the space</param>
        public void DropType(Type type, bool claimFreespace)
        {
            lock (_locker)
            {
                if (!opened)
                {
                    throw new SiaqodbException("Database is closed, call method Open() to open it!");
                }
                SqoTypeInfo ti = GetSqoTypeInfo(type);
                storageEngine.DropType(ti,claimFreespace);
                indexManager.DropIndexes(ti,claimFreespace);
                this.metaCache.Remove(type);
            }
        }
#if ASYNC
        /// <summary>
        ///  Delete all objects of Type provided
        /// </summary>
        /// <param name="type">Type of objects to be deleted</param>
        /// <param name="claimFreespace">If this is TRUE all dynamic length data associated with objects will be marked as free and Shrink method is able to free the space</param>
        public async Task DropTypeAsync(Type type, bool claimFreespace)
        {

            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            SqoTypeInfo ti = GetSqoTypeInfo(type);
            await storageEngine.DropTypeAsync(ti, claimFreespace);
            await indexManager.DropIndexesAsync(ti, claimFreespace);
            this.metaCache.Remove(type);

        }
#endif
		internal object LoadObjectByOID(Type type,int oid)
		{
			SqoTypeInfo ti = this.GetSqoTypeInfo(type);
			return storageEngine.LoadObjectByOID(ti, oid);
		}
#if ASYNC
        internal async Task<object> LoadObjectByOIDAsync(Type type, int oid)
        {
            SqoTypeInfo ti = this.GetSqoTypeInfo(type);
            return await storageEngine.LoadObjectByOIDAsync(ti, oid);
        }
#endif
        /// <summary>
        /// Return all Types from database folder
        /// </summary>
        /// <returns>List of MetaType objects</returns>
		public List<MetaType> GetAllTypes()
		{
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            List<MetaType> list = new List<MetaType>();
            List<SqoTypeInfo> tiList = storageEngine.LoadAllTypesForObjectManager();
            foreach (SqoTypeInfo ti in tiList)
			{
				MetaType mt= new MetaType();
				mt.Name = ti.TypeName;
                mt.TypeID = ti.Header.TID;
                mt.FileName = ti.FileNameForManager;
				foreach (FieldSqoInfo fi in ti.Fields)
				{
					MetaField mf = new MetaField();
                    
					mf.FieldType = fi.AttributeType;
					mf.Name = fi.Name;
					mt.Fields.Add(mf);
				}
				list.Add(mt);
			}
			return list;
		}
#if ASYNC
        /// <summary>
        /// Return all Types from database folder
        /// </summary>
        /// <returns>List of MetaType objects</returns>
        public async Task<List<MetaType>> GetAllTypesAsync()
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            List<MetaType> list = new List<MetaType>();
            List<SqoTypeInfo> tiList = await storageEngine.LoadAllTypesForObjectManagerAsync();
            foreach (SqoTypeInfo ti in tiList)
            {
                MetaType mt = new MetaType();
                mt.Name = ti.TypeName;
                mt.TypeID = ti.Header.TID;
                mt.FileName = ti.FileNameForManager;
                foreach (FieldSqoInfo fi in ti.Fields)
                {
                    MetaField mf = new MetaField();

                    mf.FieldType = fi.AttributeType;
                    mf.Name = fi.Name;
                    mt.Fields.Add(mf);
                }
                list.Add(mt);
            }
            return list;
        }
#endif
        /// <summary>
        /// Return number of objects of Type provided
        /// </summary>
        /// <typeparam name="T">Type of objects</typeparam>
        /// <returns></returns>
        public int Count<T>()
        {
            lock (_locker)
            {
                if (!opened)
                {
                    throw new SiaqodbException("Database is closed, call method Open() to open it!");
                }
                SqoTypeInfo ti = this.GetSqoTypeInfo<T>();
                return storageEngine.Count(ti);
            }
        }
#if ASYNC
        /// <summary>
        /// Return number of objects of Type provided
        /// </summary>
        /// <typeparam name="T">Type of objects</typeparam>
        /// <returns></returns>
        public async Task<int> CountAsync<T>()
        {
             bool locked = false; await _lockerAsync.LockAsync(typeof(T), out locked);
             try
             {
                 if (!opened)
                 {
                     throw new SiaqodbException("Database is closed, call method Open() to open it!");
                 }
                 SqoTypeInfo ti = this.GetSqoTypeInfo<T>();
                 return await storageEngine.CountAsync(ti);
             }
             finally { if (locked) _lockerAsync.Release(); }

        }
#endif
#if !UNITY3D
        /// <summary>
        /// Export to XML all objects of Type provided from database
        /// </summary>
        /// <typeparam name="T">Type of objects to be exported</typeparam>
        /// <param name="writer">XmlWriter</param>
        public void ExportToXML<T>(System.Xml.XmlWriter writer) 
        {
            IObjectList<T> objects = this.LoadAll<T>();
            this.ExportToXML<T>(writer, objects);
        }
        /// <summary>
        /// Export to XML list of objects provided
        /// </summary>
        /// <typeparam name="T">Type of objects</typeparam>
        /// <param name="writer">XmlWriter</param>
        /// <param name="objects">list of objects to be exported</param>
        public void ExportToXML<T>(System.Xml.XmlWriter writer,IList<T> objects) 
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            if (writer == null)
            {
                throw new ArgumentNullException();
            }
            if (objects == null)
            {
                throw new ArgumentNullException();
            }
            ImportExport.ExportToXML<T>(writer,objects,this);
        }
        /// <summary>
        /// Import from XML objects and return a list of them
        /// </summary>
        /// <typeparam name="T">Type of objects to be imported</typeparam>
        /// <param name="reader">XmlReader</param>
        /// <returns>List of objects imported</returns>
        public IObjectList<T> ImportFromXML<T>(System.Xml.XmlReader reader) 
        {
            if (reader == null)
            {
                throw new ArgumentNullException();
            }
            return ImportExport.ImportFromXML<T>(reader, this);
        }
      
        /// <summary>
        /// Import from XML objects and return a list and save into database
        /// </summary>
        /// <typeparam name="T">Type of objects to be imported</typeparam>
        /// <param name="reader">XmlReader</param>
        /// <param name="importIntoDB">if TRUE objects are saved also in database</param>
        /// <returns>List of objects imported</returns>
        public IObjectList<T> ImportFromXML<T>(System.Xml.XmlReader reader, bool importIntoDB) 
        {
            IObjectList<T> objects = this.ImportFromXML<T>(reader);
            if (importIntoDB)
            {
                foreach (T o in objects)
                {
                    this.StoreObject(o);
                }
            }
            return objects;
        }
#endif
     
        
       
        /// <summary>
        /// Update an object in database by a certain Field(eq: ID that come from a server)
        /// </summary>
        /// <param name="fieldName">FieldName by which update is made(eq an ID)</param>
        /// <param name="obj">object that has all values but not OID to update it in database</param>
        /// <returns>true if object was updated and false if object was not found in database</returns>
        public bool UpdateObjectBy(string fieldName, object obj)
        {
            return this.UpdateObjectBy(obj, fieldName);
        }
#if ASYNC

        /// <summary>
        /// Update an object in database by a certain Field(eq: ID that come from a server)
        /// </summary>
        /// <param name="fieldName">FieldName by which update is made(eq an ID)</param>
        /// <param name="obj">object that has all values but not OID to update it in database</param>
        /// <returns>true if object was updated and false if object was not found in database</returns>
        public async Task<bool> UpdateObjectByAsync(string fieldName, object obj)
        {
            return await this.UpdateObjectByAsync(obj, fieldName);
        }
#endif
        /// <summary>
        /// Update an object in database by certain Fields(eq: ID that come from a server)
        /// </summary>
        /// <param name="fieldNames">name of fields by which update is made(eq an ID)</param>
        /// <param name="obj">object that has all values but not OID to update it in database</param>
        /// <returns>true if object was updated and false if object was not found in database</returns>
        
        public bool UpdateObjectBy(object obj,params string[] fieldNames)
        {
            return this.UpdateObjectBy(obj, null, fieldNames);
        }
#if ASYNC
        /// <summary>
        /// Update an object in database by certain Fields(eq: ID that come from a server)
        /// </summary>
        /// <param name="fieldNames">name of fields by which update is made(eq an ID)</param>
        /// <param name="obj">object that has all values but not OID to update it in database</param>
        /// <returns>true if object was updated and false if object was not found in database</returns>

        public async Task<bool> UpdateObjectByAsync(object obj, params string[] fieldNames)
        {
            return await this.UpdateObjectByAsync(obj, null, fieldNames);
        }
#endif
        /// <summary>
        /// Update an object in database by certain Fields(eq: ID that come from a server)
        /// </summary>
        /// <param name="fieldNames">name of fields by which update is made(eq an ID)</param>
        /// <param name="obj">object that has all values but not OID to update it in database</param>
        /// <param name="transaction">Transaction object</param>
        /// <returns>true if object was updated and false if object was not found in database</returns>
        public bool UpdateObjectBy(object obj, Sqo.Transactions.Transaction transaction, params string[] fieldNames)
        {
            lock (_locker)
            {
                if (fieldNames == null || fieldNames.Length == 0)
                {
                    throw new ArgumentNullException("fieldsName");
                }

                if (transaction != null)
                {
                    if (transaction.status == Transactions.TransactionStatus.Closed)
                    {
                        throw new SiaqodbException("Transaction closed!");
                    }
                }


                SqoTypeInfo ti = GetSqoTypeInfoToStoreObject(obj);
                if (ti != null)
                {

                    bool stored = storageEngine.UpdateObjectBy(fieldNames, obj, ti, transaction);

                    SavedEventsArgs saved = new SavedEventsArgs(obj.GetType(), obj);
                    saved.Inserted = false;
                    this.OnSavedObject(saved);

                    return stored;
                }
                return false;
            }
        }
#if ASYNC
        /// <summary>
        /// Update an object in database by certain Fields(eq: ID that come from a server)
        /// </summary>
        /// <param name="fieldNames">name of fields by which update is made(eq an ID)</param>
        /// <param name="obj">object that has all values but not OID to update it in database</param>
        /// <param name="transaction">Transaction object</param>
        /// <returns>true if object was updated and false if object was not found in database</returns>
        public async Task<bool> UpdateObjectByAsync(object obj, Sqo.Transactions.Transaction transaction, params string[] fieldNames)
        {
            bool locked = false; await _lockerAsync.LockAsync(obj.GetType(), out locked);
            try
            {
                if (fieldNames == null || fieldNames.Length == 0)
                {
                    throw new ArgumentNullException("fieldsName");
                }
                if (transaction != null)
                {
                    if (transaction.status == Transactions.TransactionStatus.Closed)
                    {
                        throw new SiaqodbException("Transaction closed!");
                    }
                }

                SqoTypeInfo ti = await GetSqoTypeInfoToStoreObjectAsync(obj);
                if (ti != null)
                {

                    bool stored = await storageEngine.UpdateObjectByAsync(fieldNames, obj, ti, transaction);

                    SavedEventsArgs saved = new SavedEventsArgs(obj.GetType(), obj);
                    saved.Inserted = false;
                    this.OnSavedObject(saved);

                    return stored;
                }
                return false;
            }
            finally { if (locked) _lockerAsync.Release(); }
        }
#endif
        internal bool UpdateField(int oid,MetaType metaType, string field, object value)
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            if (!cacheForManager.Contains(metaType.Name))
            {
                SqoTypeInfo ti = storageEngine.GetSqoTypeInfo(metaType.Name);
                cacheForManager.AddType(metaType.Name, ti);
            }
            SqoTypeInfo tinf = cacheForManager.GetSqoTypeInfo(metaType.Name);
            
            return storageEngine.SaveValue(oid, field, tinf,value);
           
        }
        

        #region private methods
        private bool DeleteObjInternal(object obj, SqoTypeInfo ti,Transactions.Transaction transaction)
        {
            int oid = metaCache.GetOIDOfObject(obj, ti);
            if (oid <= 0 || oid > ti.Header.numberOfRecords)
            {
                throw new SiaqodbException("Object not exists in database!");
            }

            DeletingEventsArgs delEv = new DeletingEventsArgs(ti.Type, oid);
            this.OnDeletingObject(delEv);
            if (delEv.Cancel)
            {
                return false;
            }
            if(transaction==null)
            {
                storageEngine.DeleteObject(obj, ti);
            }
            else
            {
                storageEngine.DeleteObject(obj,ti,transaction,null);
            }
            DeletedEventsArgs deletedEv = new DeletedEventsArgs(ti.Type, oid);
            this.OnDeletedObject(deletedEv);
            return true;
        }
#if ASYNC
        private async Task<bool> DeleteObjInternalAsync(object obj, SqoTypeInfo ti, Transactions.Transaction transaction)
        {
            int oid = metaCache.GetOIDOfObject(obj, ti);
            if (oid <= 0 || oid > ti.Header.numberOfRecords)
            {
                throw new SiaqodbException("Object not exists in database!");
            }

            DeletingEventsArgs delEv = new DeletingEventsArgs(ti.Type, oid);
            this.OnDeletingObject(delEv);
            if (delEv.Cancel)
            {
                return false;
            }
            if (transaction == null)
            {
                await storageEngine.DeleteObjectAsync(obj, ti);
            }
            else
            {
                await storageEngine.DeleteObjectAsync(obj, ti, transaction, null);
            }
            DeletedEventsArgs deletedEv = new DeletedEventsArgs(ti.Type, oid);
            this.OnDeletedObject(deletedEv);
            return true;
        }
#endif
        internal SqoTypeInfo CheckDBAndGetSqoTypeInfo<T>()
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            SqoTypeInfo ti = this.GetSqoTypeInfo<T>();
            if (ti.IsOld)
            {
                throw new TypeChangedException("Actual runtime Type:" + ti.Type.Name + "is different than Type stored in DB, in current version is not supported automatically type changing, to fix this, modify your class like it was when u saved objects in DB");
            }
            return ti;
        }
        private SqoTypeInfo CheckDBAndGetSqoTypeInfo(Type type)
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            SqoTypeInfo ti = this.GetSqoTypeInfo(type);
            if (ti.IsOld)
            {
                throw new TypeChangedException("Actual runtime Type:" + ti.Type.Name + "is different than Type stored in DB, in current version is not supported automatically type changing, to fix this, modify your class like it was when u saved objects in DB");
            }
            return ti;
        }
    #endregion


        internal List<object> LoadDirtyObjects(Type type)
        {
            lock (_locker)
            {
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo(type);
                Where w = new Where("isDirty", OperationType.Equal, true);
                w.StorageEngine = this.storageEngine;
                w.ParentSqoTypeInfo = ti;
                w.ParentType.Add(w.ParentSqoTypeInfo.Type);
                List<int> oidsDirty = w.GetOIDs();

                Where wDelete = new Where("isTombstone", OperationType.Equal, true);
                wDelete.StorageEngine = this.storageEngine;
                wDelete.ParentSqoTypeInfo = ti;
                wDelete.ParentType.Add(wDelete.ParentSqoTypeInfo.Type);
                List<int> oidsDeleted = this.storageEngine.LoadFilteredDeletedOids(wDelete, ti);

                oidsDirty.AddRange(oidsDeleted);

                return this.storageEngine.LoadByOIDs(oidsDirty, ti);
            }
        }
#if ASYNC
        internal async Task<List<object>> LoadDirtyObjectsAsync(Type type)
        {
            bool locked = false; 
            await _lockerAsync.LockAsync(type,out locked);
            try
            {
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo(type);
                Where w = new Where("isDirty", OperationType.Equal, true);
                w.StorageEngine = this.storageEngine;
                w.ParentSqoTypeInfo = ti;
                w.ParentType.Add(w.ParentSqoTypeInfo.Type);
                List<int> oidsDirty = await w.GetOIDsAsync();

                Where wDelete = new Where("isTombstone", OperationType.Equal, true);
                wDelete.StorageEngine = this.storageEngine;
                wDelete.ParentSqoTypeInfo = ti;
                wDelete.ParentType.Add(wDelete.ParentSqoTypeInfo.Type);
                List<int> oidsDeleted = await this.storageEngine.LoadFilteredDeletedOidsAsync(wDelete, ti);

                oidsDirty.AddRange(oidsDeleted);

                return await this.storageEngine.LoadByOIDsAsync(oidsDirty, ti);
            }
            finally { if (locked) _lockerAsync.Release(); }
        }
#endif

        /// <summary>
        /// return current database path
        /// </summary>
        /// <returns>The database folder path</returns>
        public string GetDBPath()
        {
            return this.path;
        }
        /// <summary>
        /// Start a database Transaction to be used on insert/update/delete objects
        /// </summary>
        /// <returns> Transaction object</returns>
        public Transactions.Transaction BeginTransaction()
        {
            this.circularRefCache.Clear();
            return Transactions.TransactionManager.BeginTransaction(this);
        }
        internal Transactions.TransactionsStorage GetTransactionLogStorage()
        {
            return storageEngine.GetTransactionLogStorage();
        }
        internal void DropTransactionLog()
        {

            storageEngine.DropTransactionLog();

        }
        private void RecoverAfterCrash()
        {
            SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<Transactions.TransactionObjectHeader>();

            SqoTypeInfo tiTypeHeader = CheckDBAndGetSqoTypeInfo<Transactions.TransactionTypeHeader>();

            storageEngine.RecoverAfterCrash(ti,tiTypeHeader);
        }
#if ASYNC
        private async Task RecoverAfterCrashAsync()
        {
            SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<Transactions.TransactionObjectHeader>();

            SqoTypeInfo tiTypeHeader = CheckDBAndGetSqoTypeInfo<Transactions.TransactionTypeHeader>();

            await storageEngine.RecoverAfterCrashAsync(ti, tiTypeHeader);
        }
#endif
        internal void TransactionCommitStatus(bool started)
        {
            storageEngine.TransactionCommitStatus(started);
        }
       
        internal void Flush<T>()
        {
            SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
            storageEngine.Flush(ti);

        }
#if ASYNC
        internal async Task FlushAsync<T>()
        {
            SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
            await storageEngine.FlushAsync(ti);

        }
#endif

        internal void DeleteObjectByMeta(int oid, MetaType metaType)
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            if (!cacheForManager.Contains(metaType.Name))
            {
                SqoTypeInfo ti = storageEngine.GetSqoTypeInfo(metaType.Name);
                cacheForManager.AddType(metaType.Name, ti);
            }
            SqoTypeInfo tinf = cacheForManager.GetSqoTypeInfo(metaType.Name);

            storageEngine.DeleteObjectByOID(oid, tinf);
        }

        internal int InsertObjectByMeta(MetaType metaType)
        {
            if (!opened)
            {
                throw new SiaqodbException("Database is closed, call method Open() to open it!");
            }
            if (!cacheForManager.Contains(metaType.Name))
            {
                SqoTypeInfo ti = storageEngine.GetSqoTypeInfo(metaType.Name);
                cacheForManager.AddType(metaType.Name, ti);
            }
            SqoTypeInfo tinf = cacheForManager.GetSqoTypeInfo(metaType.Name);
            return storageEngine.InsertObjectByMeta(tinf);
        }

        internal Sqo.Indexes.IBTree GetIndex(string field, Type type)
        {
            SqoTypeInfo ti = this.GetSqoTypeInfo(type);
            return indexManager.GetIndex(field, ti);
        }
        /// <summary>
        /// Get a list of unique values for a field index
        /// </summary>
        /// <typeparam name="T">Type where index is defined</typeparam>
        /// <typeparam name="TIndex">Type of field indexed</typeparam>
        /// <param name="fieldName">Name of field or automatic property which is indexed</param>
        /// <returns></returns>
        public IList<TIndex> LoadIndexValues<T, TIndex>(string fieldName)
        {
            string fieldNameAsInDB = MetaHelper.GetFieldAsInDB(fieldName, typeof(T));
            Sqo.Indexes.IBTree index = this.GetIndex(fieldNameAsInDB, typeof(T));
            if (index != null)
            {
                Sqo.Indexes.IBTree<TIndex> indexT=(Sqo.Indexes.IBTree<TIndex>)index;
                return indexT.DumpKeys();
            }

            throw new SiaqodbException("Index not exists for field:" + fieldName);
            
        }
        /// <summary>
        /// Load all objects in Lazy mode, objects are activated/read from db when it is accessed
        /// by index or by enumerator
        /// </summary>
        /// <typeparam name="T">Type of objects to be loaded from database</typeparam>
        /// <returns>LazyObjectList of objects</returns>
        public IObjectList<T> LoadAllLazy<T>()
        {
            lock (_locker)
            {
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                List<int> oids = storageEngine.LoadAllOIDs(ti);
                return new LazyObjectList<T>(this, oids);
            }
        }
#if ASYNC
        /// <summary>
        /// Load all objects in Lazy mode, objects are activated/read from db when it is accessed
        /// by index or by enumerator
        /// </summary>
        /// <typeparam name="T">Type of objects to be loaded from database</typeparam>
        /// <returns>LazyObjectList of objects</returns>
        public async Task<IObjectList<T>> LoadAllLazyAsync<T>()
        {
            bool locked = false; await _lockerAsync.LockAsync(typeof(T), out locked);
            try
            {
                SqoTypeInfo ti = CheckDBAndGetSqoTypeInfo<T>();
                List<int> oids = await storageEngine.LoadAllOIDsAsync(ti);
                return new LazyObjectList<T>(this, oids);
            }
            finally { if (locked) _lockerAsync.Release(); }
        }
#endif
        internal void LoadObjectOIDAndTID(int oid, string fieldName, MetaType mt,ref List<int> listOIDs,ref int TID)
        {
            if (!cacheForManager.Contains(mt.Name))
            {
                SqoTypeInfo ti = storageEngine.GetSqoTypeInfo(mt.Name);
                cacheForManager.AddType(mt.Name, ti);
            }
            SqoTypeInfo tinf = cacheForManager.GetSqoTypeInfo(mt.Name);
            FieldSqoInfo fi= MetaHelper.FindField(tinf.Fields, fieldName);
            if (fi.AttributeTypeId == MetaExtractor.complexID || fi.AttributeTypeId==MetaExtractor.documentID)
            {
                KeyValuePair<int, int> kv = storageEngine.LoadOIDAndTID(oid, fi, tinf);
                listOIDs.Add(kv.Key);
                TID = kv.Value;
            }
            else if(fi.AttributeTypeId-MetaExtractor.ArrayTypeIDExtra == MetaExtractor.complexID)
            {
                List<KeyValuePair<int, int>> list = storageEngine.LoadComplexArray(oid, fi, tinf);
                if (list.Count > 0)
                {
                    TID = list[0].Value;
                    foreach (KeyValuePair<int,int> kv in list)
                    {
                        listOIDs.Add(kv.Key);
                    }
                }
            }
        }
       

        internal void LoadTIDofComplex(int oid, string fieldName, MetaType mt, ref int TID, ref bool isArray)
        {
            if (!cacheForManager.Contains(mt.Name))
            {
                SqoTypeInfo ti = storageEngine.GetSqoTypeInfo(mt.Name);
                cacheForManager.AddType(mt.Name, ti);
            }
            SqoTypeInfo tinf = cacheForManager.GetSqoTypeInfo(mt.Name);
            FieldSqoInfo fi = MetaHelper.FindField(tinf.Fields, fieldName);
            if (fi.AttributeTypeId == MetaExtractor.complexID || fi.AttributeTypeId == MetaExtractor.documentID)
            {
                KeyValuePair<int, int> kv = storageEngine.LoadOIDAndTID(oid, fi, tinf);
                TID = kv.Value;
                isArray = false;
            }
            else if (fi.AttributeTypeId - MetaExtractor.ArrayTypeIDExtra == MetaExtractor.complexID)
            {
                isArray = true;
                TID = storageEngine.LoadComplexArrayTID(oid, fi, tinf);
            }
            else if (fi.AttributeTypeId - MetaExtractor.ArrayTypeIDExtra == MetaExtractor.jaggedArrayID)
            {
                isArray = true;
                TID = -32;
            }
            else if (fi.AttributeTypeId == MetaExtractor.dictionaryID)
            {
                TID = -31;
            }
        }

        public void StartBulkInsert(params Type[] types)
        {
            Monitor.Enter(_locker);
            foreach (Type t in types)
            {
                SqoTypeInfo ti = this.GetSqoTypeInfoToStoreObject(t);
                if (ti != null)
                {
                    this.PutIndexPersiststenceState(ti, false);
                }
            }
        }
#if ASYNC
        public async Task StartBulkInsertAsync(params Type[] types)
        {
            foreach (Type t in types)
            {
                SqoTypeInfo ti = await this.GetSqoTypeInfoToStoreObjectAsync(t);
                if (ti != null)
                {
                    this.PutIndexPersiststenceState(ti, false);
                }
            }
        }
#endif
        public void EndBulkInsert(params Type[] types)
        {
            foreach (Type t in types)
            {
                SqoTypeInfo ti = this.GetSqoTypeInfoToStoreObject(t);
                if (ti != null)
                {
                    this.PutIndexPersiststenceState(ti, true);
                    this.PersistIndexDirtyNodes(ti);
                }
            }
            Monitor.Exit(_locker);
        }
#if ASYNC
        public async Task EndBulkInsertAsync(params Type[] types)
        {
            foreach (Type t in types)
            {
                SqoTypeInfo ti = await this.GetSqoTypeInfoToStoreObjectAsync(t);
                if (ti != null)
                {
                    this.PutIndexPersiststenceState(ti, true);
                    this.PersistIndexDirtyNodes(ti);
                }
            }
            
        }
#endif

        #region Indexes
        internal bool IsObjectDeleted(int oid, SqoTypeInfo ti)
        {
            return storageEngine.IsObjectDeleted(oid, ti);
        }
#if ASYNC
        internal async Task<bool> IsObjectDeletedAsync(int oid, SqoTypeInfo ti)
        {
            return await storageEngine.IsObjectDeletedAsync(oid, ti);
        }
#endif
        internal void PutIndexPersiststenceState(SqoTypeInfo ti, bool on)
        {
            indexManager.PutIndexPersistenceOnOff(ti, on);
        }
        internal void PersistIndexDirtyNodes(SqoTypeInfo ti)
        {
            indexManager.Persist(ti);
        }
#if ASYNC
        internal async Task PersistIndexDirtyNodesAsync(SqoTypeInfo ti)
        {
            await indexManager.PersistAsync(ti);
        }
#endif
        #endregion

        internal int AllocateNewOID<T>()
        {
            SqoTypeInfo ti = this.GetSqoTypeInfoToStoreObject(typeof(T));
            if (ti != null)
            {
                return storageEngine.AllocateNewOID(ti);
            }
            return 0;
        }
#if ASYNC
        internal async Task<int> AllocateNewOIDAsync<T>()
        {
            SqoTypeInfo ti =await this.GetSqoTypeInfoToStoreObjectAsync(typeof(T));
            if (ti != null)
            {
                return await storageEngine.AllocateNewOIDAsync(ti);
            }
            return 0;
        }
#endif



        internal Core.ISqoFile GetRawFile()
        {
            return storageEngine.GetRawFile();
        }

        internal void ReIndexAll(bool claimFreespace)
        {
            List<SqoTypeInfo> typesForIndexes = this.metaCache.DumpAllTypes();
            foreach (SqoTypeInfo ti in typesForIndexes)
            {
                if (ti.Type.IsGenericType() && ti.Type.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>))
                {
                    this.DropType(ti.Type, claimFreespace);
                }
            }
            indexManager.DeleteAllIndexInfo();
            this.DropType(typeof(IndexInfo2));

            this.indexManager.BuildAllIndexes(typesForIndexes);
        }
#if ASYNC

        internal async Task ReIndexAllAsync(bool claimFreespace)
        {
            List<SqoTypeInfo> typesForIndexes = this.metaCache.DumpAllTypes();
            foreach (SqoTypeInfo ti in typesForIndexes)
            {
                if (ti.Type.IsGenericType() && ti.Type.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>))
                {
                    await this.DropTypeAsync(ti.Type, claimFreespace);
                }
            }
            indexManager.DeleteAllIndexInfo();
            await this.DropTypeAsync(typeof(IndexInfo2));

            await this.indexManager.BuildAllIndexesAsync(typesForIndexes);
        }
#endif
        internal List<int> GetUsedRawdataInfoOIDS()
        { 
             List<SqoTypeInfo> existingTypes = this.metaCache.DumpAllTypes();
             List<int> oids = new List<int>();
             foreach (SqoTypeInfo ti in existingTypes)
             { 
                oids.AddRange(storageEngine.GetUsedRawdataInfoOIDs(ti));
             }
             return oids;
        }
#if ASYNC
        internal async Task<List<int>> GetUsedRawdataInfoOIDSAsync()
        {
            List<SqoTypeInfo> existingTypes = this.metaCache.DumpAllTypes();
            List<int> oids = new List<int>();
            foreach (SqoTypeInfo ti in existingTypes)
            {
                List<int> l=await storageEngine.GetUsedRawdataInfoOIDsAsync(ti);
                oids.AddRange(l);
            }
            return oids;
        }
#endif
        internal void MarkRawInfoAsFree(List<int> rawdataInfoOIDs)
        {
            this.storageEngine.MarkRawInfoAsFree(rawdataInfoOIDs);
        }
#if ASYNC
        internal async Task MarkRawInfoAsFreeAsync(List<int> rawdataInfoOIDs)
        {
            await this.storageEngine.MarkRawInfoAsFreeAsync(rawdataInfoOIDs);
        }
#endif
        internal void RepairAllTypes()
        {
            List<SqoTypeInfo> existingTypes = this.metaCache.DumpAllTypes();

            foreach (SqoTypeInfo ti in existingTypes)
            {
                List<int> oids = storageEngine.LoadAllOIDs(ti);
                foreach (int oid in oids)
                {
                    object obj = storageEngine.LoadObjectByOID(ti, oid);
                }

            }
        }
#if ASYNC
        internal async Task RepairAllTypesAsync()
        {
            List<SqoTypeInfo> existingTypes = this.metaCache.DumpAllTypes();

            foreach (SqoTypeInfo ti in existingTypes)
            {
                List<int> oids = await storageEngine.LoadAllOIDsAsync(ti);
                foreach (int oid in oids)
                {
                    object obj = await storageEngine.LoadObjectByOIDAsync(ti, oid);
                }

            }
        }
#endif
        internal void ShrinkAllTypes()
        {
            lock (_locker)
            {
                List<SqoTypeInfo> existingTypes = this.metaCache.DumpAllTypes();
               

                foreach (SqoTypeInfo ti in existingTypes)
                {
                    if (ti.Type == typeof(Sqo.MetaObjects.RawdataInfo) ||
                        ti.Type == typeof(Sqo.Indexes.IndexInfo2) ||
                        (ti.Type.IsGenericType() && ti.Type.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>)))
                    {
                        continue;
                    }

                    List<int> oids = storageEngine.LoadAllOIDs(ti);
                    Dictionary<int, byte[]> objectBytes = new Dictionary<int, byte[]>();
                    foreach (int oid in oids)
                    {
                        byte[] obj = storageEngine.GetObjectBytes(oid, ti);
                        objectBytes.Add(oid, obj);
                    }
                    storageEngine.SetFileLength(ti.Header.headerSize, ti);
                    ti.Header.numberOfRecords=0;
                    if (oids.Count == 0)
                    {
                        storageEngine.SaveType(ti);//to save nrRecords
                    }
                    foreach (int oid in objectBytes.Keys)
                    {
                        int newOID = storageEngine.SaveObjectBytes(objectBytes[oid], ti);
                        ShrinkResult shrinkResult = new ShrinkResult() {
                            Old_OID = oid, 
                            New_OID = newOID, 
                            TID = ti.Header.TID };
                        this.StoreObject(shrinkResult);
                    }
                    
                }
                IList<ShrinkResult> shrinkResults = this.LoadAll<ShrinkResult>();
                if (shrinkResults.Count > 0)
                {
                    foreach (SqoTypeInfo ti in existingTypes)
                    {
                        if (ti.Type == typeof(Sqo.MetaObjects.RawdataInfo) ||
                            ti.Type == typeof(Sqo.Indexes.IndexInfo2) ||
                            (ti.Type.IsGenericType() && ti.Type.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>)))
                        {
                            continue;
                        }
                        storageEngine.AdjustComplexFieldsAfterShrink(ti, shrinkResults);
                    }
                }
                DropType(typeof(ShrinkResult));
            }
        }
#if ASYNC
        internal async Task ShrinkAllTypesAsync()
        {

            List<SqoTypeInfo> existingTypes = this.metaCache.DumpAllTypes();


            foreach (SqoTypeInfo ti in existingTypes)
            {
                if (ti.Type == typeof(Sqo.MetaObjects.RawdataInfo) ||
                    ti.Type == typeof(Sqo.Indexes.IndexInfo2) ||
                    (ti.Type.IsGenericType() && ti.Type.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>)))
                {
                    continue;
                }

                List<int> oids = await storageEngine.LoadAllOIDsAsync(ti);
                Dictionary<int, byte[]> objectBytes = new Dictionary<int, byte[]>();
                foreach (int oid in oids)
                {
                    byte[] obj = await storageEngine.GetObjectBytesAsync(oid, ti);
                    objectBytes.Add(oid, obj);
                }
                storageEngine.SetFileLength(ti.Header.headerSize, ti);
                ti.Header.numberOfRecords = 0;
                if (oids.Count == 0)
                {
                    await storageEngine.SaveTypeAsync(ti);//to save nrRecords
                }
                foreach (int oid in objectBytes.Keys)
                {
                    int newOID = await storageEngine.SaveObjectBytesAsync(objectBytes[oid], ti);
                    ShrinkResult shrinkResult = new ShrinkResult()
                    {
                        Old_OID = oid,
                        New_OID = newOID,
                        TID = ti.Header.TID
                    };
                    await this.StoreObjectAsync(shrinkResult);
                }

            }
            IList<ShrinkResult> shrinkResults = await this.LoadAllAsync<ShrinkResult>();
            if (shrinkResults.Count > 0)
            {
                foreach (SqoTypeInfo ti in existingTypes)
                {
                    if (ti.Type == typeof(Sqo.MetaObjects.RawdataInfo) ||
                        ti.Type == typeof(Sqo.Indexes.IndexInfo2) ||
                        (ti.Type.IsGenericType() && ti.Type.GetGenericTypeDefinition() == typeof(Indexes.BTreeNode<>)))
                    {
                        continue;
                    }
                    await storageEngine.AdjustComplexFieldsAfterShrinkAsync(ti, shrinkResults);
                }
            }
            await DropTypeAsync(typeof(ShrinkResult));

        }

#endif
        /// <summary>
        /// Get OID of object, if the Type of object has not defined OID property then object and OID are weak cached during object load from database and this value is returned,
        /// otherwise it is returned value of the OID property 
        /// </summary>
        /// <param name="obj">The object for which OID is returned</param>
        /// <returns>The OID associated with object that is stored in database</returns>
        public int GetOID(object obj)
        {
            lock (_locker)
            {
                if (obj == null)
                {
                    throw new ArgumentNullException("obj");
                }

                SqoTypeInfo ti = this.CheckDBAndGetSqoTypeInfo(obj.GetType());
                return metaCache.GetOIDOfObject(obj, ti);
            }
        }
        internal void SetDatabaseFileName(string fileName, MetaType type)
        {

            Cache.CacheCustomFileNames.AddFileNameForType(type.Name, fileName, false);

        }
        internal void GetOIDForAMSByField(object obj, string fieldName)
        {
            SqoTypeInfo ti = GetSqoTypeInfoToStoreObject(obj.GetType());
            List<int> oids = storageEngine.LoadOidsByField(ti, fieldName, obj);

            if (oids.Count > 1)
            {
                throw new SiaqodbException("Many objects with this field value exists is database.");
            }
            else if (oids.Count == 1)
            {
                metaCache.SetOIDToObject(obj, oids[0], ti);
            }
        }
        private void CheckLic()
        {
#if TRIAL

#elif WinRT
             bool li = WinRTLicenseChecker.LicenseValid();
#elif SILVERLIGHT 
            SilvLicenseChecker.LicenseValid();
#elif UNITY3D || CF || MONODROID
            SqoUnity3DLic.LicenseValid();
#else
            try
            {
                System.ComponentModel.License license = System.ComponentModel.LicenseManager.Validate(typeof(Siaqodb), this);
            }
            catch (Exception ex)
            {
                SqoUnity3DLic.LicenseValid();
            }
            
#endif
        }
       
    }
   
}
