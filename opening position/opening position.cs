using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    
    [Robot(AccessRights = AccessRights.None)]
    public class openingposition : Robot
    {
        [Parameter("Account Risk Percentage", DefaultValue = 2.0, MinValue = 0.1, MaxValue = 10.0)]
        public double AccountRiskPercentage { get; set; }
        
        [Parameter("Return ratio", DefaultValue = 4, MinValue = 0.5, MaxValue = 200)]
        public double ReturnRation { get; set; }
        
        [Parameter("Buy order key", Group = "Key definition",  DefaultValue = Key.None)]
        public Key BuyKey { get; set; }
        
        [Parameter("(Optional) Buy order prefix key", Group = "Key definition",  DefaultValue = ModifierKeys.None)]
        public ModifierKeys BuyKeyPrefix { get; set; }
        
        [Parameter("Sell order key", Group = "Key definition",  DefaultValue = Key.None)]
        public Key SellKey { get; set; }
        
        [Parameter("(Optional) Sell order prefix key", Group = "Key definition",  DefaultValue = ModifierKeys.None)]
        public ModifierKeys SellKeyPrefix { get; set; }
        
        private TextBox stopLossTextBox;
        private TextBlock postionInfoTextBlock;
        private Button buyButton;
        private Button sellButton;
        
        private double GetPositionSize(double riskPercentage, double stopLossPips)
        {
            var accountEquity = Account.Equity;
            var riskAmount = accountEquity * ( riskPercentage / 100.0);

            var volume = riskAmount / Symbol.PipValue / stopLossPips;
            var positionSize = Symbol.NormalizeVolumeInUnits(volume, RoundingMode.ToNearest);

            return positionSize;
        }

        private bool TryGetStopLossPips(TradeType direction, out double stopLoss)
        {
            if (!double.TryParse(stopLossTextBox.Text, out var stopLossPrice))
            {
                stopLoss = 0;
                return false;
            }

            var isBuyTrade = direction == TradeType.Buy;

            var stopLossSpace = (isBuyTrade ? Symbol.Ask : Symbol.Bid) - stopLossPrice;

            if ((stopLossSpace < 0 && isBuyTrade) || (stopLossSpace >= 0 && !isBuyTrade))
            {
                stopLoss = 0;
                return false;
            }
            
            stopLoss = Math.Abs(stopLossSpace) / Symbol.PipSize;
            
            return true;
        }

        private Action<T> HandleClick<T>(TradeType direction, string text)
        {
            return (args) =>
            {
                if (!TryGetStopLossPips(direction, out var stopLoss))
                {
                    Print("Can't create stoploss in place: " + stopLossTextBox.Text + " for position direction " + direction);
                    return;
                }
                double positionSize = GetPositionSize(AccountRiskPercentage, stopLoss);
                double takeProfit = ReturnRation * stopLoss;
                
                ExecuteMarketOrder(direction, SymbolName, positionSize, SymbolName + " order dir " + text, stopLoss, takeProfit);
            };
        }

        private void HandleMouseDown(ChartMouseEventArgs args)
        {
            stopLossTextBox.Text = Math.Round(args.YValue, 2).ToString(CultureInfo.CurrentCulture);
        }


        protected override void OnStart()
        {
            var row = new StackPanel
            {
                Orientation=Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Top = 10,
                Left = 20,
                Width = 500,
                MaxHeight = 80
            };

            var column = new WrapPanel
            {
                Orientation = Orientation.Vertical,
                Width = 200,
                Height = 500
            };
            

            stopLossTextBox = new TextBox
            {
                Width = 200,
                Margin = "5 10 0 0",
                Text = Symbol.Bid.ToString(CultureInfo.CurrentCulture),
            };

            postionInfoTextBlock = new TextBlock
            {
                Width = 200,
                MaxHeight = 300,
                Margin = "5 10 0 0",
                ForegroundColor = Color.White,
                LineStackingStrategy = LineStackingStrategy.MaxHeight
            };
            
            column.AddChild(stopLossTextBox);
            column.AddChild(postionInfoTextBlock);
            
            row.AddChild(column);

            var margins = "10 10 0 0";

            buyButton = new Button
            {
                BackgroundColor = Color.Green,
                Opacity = .80,
                Margin = margins,
                Text = "Buy",
                Height = 40
            };

            buyButton.Click += HandleClick<ButtonClickEventArgs>(TradeType.Buy, "Buy");

            if (BuyKey != Key.None)
            {
                Chart.AddHotkey(HandleClick<ChartKeyboardEventArgs>(TradeType.Buy, "Buy"), BuyKey, BuyKeyPrefix);
            }
            
            sellButton = new Button
            {
                BackgroundColor = Color.Red,
                Opacity = .80,
                Margin = margins,
                Text = "Sell",
                Height = 40
            };

            sellButton.Click += HandleClick<ButtonClickEventArgs>(TradeType.Sell, "Sell");
            
            if (SellKey != Key.None)
            {
                Chart.AddHotkey(HandleClick<ChartKeyboardEventArgs>(TradeType.Sell, "Sell"), SellKey, SellKeyPrefix);
            }
            
            row.AddChild(buyButton);
            row.AddChild(sellButton);
            
            Chart.AddControl(row);

            Chart.MouseDown += HandleMouseDown;
        }

        protected override void OnTick()
        {
            if (!double.TryParse(stopLossTextBox.Text, out var stopLossPrice))
            {
                return ;
            }
            
            TradeType direction = stopLossPrice - Symbol.Bid >= 0 ? TradeType.Sell : TradeType.Buy;
            
            if (!TryGetStopLossPips(direction, out var stopLoss))
            {
                return;
            }
            
            double positionSize = GetPositionSize(AccountRiskPercentage, stopLoss);
            double takeProfit = ReturnRation * stopLoss;
            double tpPrice = direction == TradeType.Buy ? Symbol.Bid + takeProfit * Symbol.PipSize : Symbol.Ask - takeProfit * Symbol.PipSize;

            var lots = Symbol.VolumeInUnitsToQuantity(positionSize);

            postionInfoTextBlock.Text = 
                "Direction: " + direction + 
                "\nLots: " + lots +
                "\nTake profit price: " + Math.Round(tpPrice, 2);
        }

        protected override void OnStop()
        {
            // Handle cBot stop here
        }
    }
}