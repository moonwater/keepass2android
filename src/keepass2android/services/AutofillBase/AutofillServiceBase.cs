﻿using System;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Service.Autofill;
using Android.Util;
using Android.Views.Autofill;
using Android.Widget;

namespace keepass2android.services.AutofillBase
{
    public interface IAutofillIntentBuilder
    {
        IntentSender GetAuthIntentSenderForResponse(Context context, string query, bool isManualRequest);
        Intent GetRestartAppIntent(Context context);

        int AppIconResource { get; }
    }

    public abstract class AutofillServiceBase: AutofillService
    {
        public AutofillServiceBase()
        {
            
        }

        public AutofillServiceBase(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }


        public override void OnFillRequest(FillRequest request, CancellationSignal cancellationSignal, FillCallback callback)
        {
            bool isManual = (request.Flags & FillRequest.FlagManualRequest) != 0;
            CommonUtil.logd( "onFillRequest " + (isManual ? "manual" : "auto"));
            var structure = request.FillContexts[request.FillContexts.Count - 1].Structure;

            //TODO support package signature verification as soon as this is supported in Keepass storage

            var clientState = request.ClientState;
            CommonUtil.logd( "onFillRequest(): data=" + CommonUtil.BundleToString(clientState));


            cancellationSignal.CancelEvent += (sender, e) => {
                Log.Warn(CommonUtil.Tag, "Cancel autofill not implemented yet.");
            };
            // Parse AutoFill data in Activity
            string query = null;
            var parser = new StructureParser(this, structure);
            try
            {
                query = parser.ParseForFill(isManual);
                
            }
            catch (Java.Lang.SecurityException e)
            {
                Log.Warn(CommonUtil.Tag, "Security exception handling request");
                callback.OnFailure(e.Message);
                return;
            }
            
            AutofillFieldMetadataCollection autofillFields = parser.AutofillFields;
            
            bool responseAuth = true;
            var autofillIds = autofillFields.GetAutofillIds();
            if (responseAuth && autofillIds.Length != 0 && CanAutofill(query))
            {
                var responseBuilder = new FillResponse.Builder();
                
                var sender = IntentBuilder.GetAuthIntentSenderForResponse(this, query, isManual);
                RemoteViews presentation = AutofillHelper.NewRemoteViews(PackageName, GetString(Resource.String.autofill_sign_in_prompt), AppNames.LauncherIcon);

                var datasetBuilder = new Dataset.Builder(presentation);
                datasetBuilder.SetAuthentication(sender);
                //need to add placeholders so we can directly fill after ChooseActivity
                foreach (var autofillId in autofillIds)
                {
                    datasetBuilder.SetValue(autofillId, AutofillValue.ForText("PLACEHOLDER"));
                }

                responseBuilder.AddDataset(datasetBuilder.Build());

                callback.OnSuccess(responseBuilder.Build());
            }
            else
            {
                var datasetAuth = true;
                var response = AutofillHelper.NewResponse(this, datasetAuth, autofillFields, null, IntentBuilder);
                callback.OnSuccess(response);
            }
        }

        private bool CanAutofill(string query)
        {
            return !(query == "androidapp://android" || query == "androidapp://"+this.PackageName);
        }

        public override void OnSaveRequest(SaveRequest request, SaveCallback callback)
        {
            //TODO implement save
            callback.OnFailure("Saving data is currently not implemented in Keepass2Android.");
        }


        public override void OnConnected()
        {
            CommonUtil.logd( "onConnected");
        }

        public override void OnDisconnected()
        {
            CommonUtil.logd( "onDisconnected");
        }

        public abstract IAutofillIntentBuilder IntentBuilder{get;}
    }
}
