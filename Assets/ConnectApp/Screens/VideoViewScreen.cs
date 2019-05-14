using System.Collections.Generic;
using ConnectApp.canvas;
using ConnectApp.components;
using ConnectApp.constants;
using Unity.UIWidgets.foundation;
using Unity.UIWidgets.painting;
using Unity.UIWidgets.rendering;
using Unity.UIWidgets.widgets;

namespace ConnectApp.screens
{
    public class VideoViewScreen : StatefulWidget
    {
        public VideoViewScreen(
            string url = null ,
            Key key = null
        ) : base(key)
        {
            this.url = url;
        }

        public readonly string url;
        
        public override State createState()
        {
            return new _VideoViewScreenState();
        }
    }

    public class _VideoViewScreenState : State<VideoViewScreen>
    {
        private bool _isFullScreen;
        
        public override Widget build(BuildContext context)
        {
            return new Container(
                color: CColors.Black,
                child: new CustomSafeArea(
                    top:!_isFullScreen,
                    bottom:!_isFullScreen,
                    child: new Container(
                        color: CColors.Black,
                        child: new Stack(
                            children:new List<Widget>
                            {
                                new Align(
                                    alignment: Alignment.center,
                                    child: new CustomVideoPlayer(
                                        widget.url,
                                        context,
                                        new Container(), 
                                        isFullScreen =>
                                        {
                                            setState(() => { _isFullScreen = isFullScreen; });
                                        },0,true
                                    )
                                ),
                                new Positioned(
                                    top:0,left:0,right:0,child:_isFullScreen?new Container(): new Container(
                                        child:new Row(
                                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                            children: new List<Widget> {
                                                new CustomButton(
                                                    onPressed: () => { Router.navigator.pop(); },
                                                    child: new Icon(
                                                        Icons.arrow_back,
                                                        size: 28,
                                                        color:CColors.White
                                                    )
                                                )
                                            }
                                        )
                                    ))
                            }
                            
                        )
                    )
                )
            );
        }
    }
}