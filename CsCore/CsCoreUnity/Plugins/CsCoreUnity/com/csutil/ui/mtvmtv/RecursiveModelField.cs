﻿using System.Threading.Tasks;
using com.csutil.model.mtvmtv;
using UnityEngine;
using UnityEngine.UI;

namespace com.csutil.ui.mtvmtv {

    public class RecursiveModelField : FieldView {

        public Button openButton;
        internal ViewModelToView viewModelToView;
        internal ViewModel recursiveViewModel;

        protected override Task Setup(string fieldName) {
            openButton.SetOnClickAction(async delegate {

                ViewModel rootViewModel = recursiveViewModel != null ? recursiveViewModel : field.objVm;
                GameObject rootContainerView = await viewModelToView.NewRootContainerView(rootViewModel);
                gameObject.GetViewStack().ShowView(rootContainerView);
                var innerContainer = await viewModelToView.SelectInnerViewContainerFromObjectFieldView(rootContainerView);
                await viewModelToView.ToView(recursiveViewModel, innerContainer);
            });
            return Task.FromResult(true);
        }
    }

}