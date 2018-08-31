using Sitecore.Commerce.Entities.Carts;
using Sitecore.Commerce.Rules.Conditions;
using Sitecore.Commerce.Services.Carts;
using Sitecore.Commerce.Services.Customers;
using Sitecore.Configuration;
using Sitecore.Diagnostics;
using Sitecore.Rules;
using Sitecore.Rules.Conditions;
using Sitecore.Sites;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sitecore.Support.Commerce.Rules.Conditions
{
    public class TotalProductQuantityCondition<T> : BaseCartMetricsCondition<T> where T : RuleContext
    {
        //this class uses implementation of default TotalProductQuantityCondition class and has overriden Execute method
        private Sitecore.Commerce.Services.Customers.CustomerServiceProvider customerServiceProvider;
        private Sitecore.Commerce.Services.Carts.CartServiceProvider cartServiceProvider;
        private Sitecore.Commerce.Contacts.ContactFactory contactFactory;

        public TotalProductQuantityCondition()
        {
            customerServiceProvider = new Sitecore.Commerce.Services.Customers.CustomerServiceProvider();
            this.cartServiceProvider = (Sitecore.Commerce.Services.Carts.CartServiceProvider)Factory.CreateObject("cartServiceProvider", true);
            this.contactFactory = (Sitecore.Commerce.Contacts.ContactFactory)Factory.CreateObject("contactFactory", true);
        }


        protected override bool Execute(T ruleContext)
        {
            SiteContext shopContext = Context.Site;

            Assert.IsNotNull(shopContext, "Context site cannot be null.");

            string userId = this.ContactFactory.GetUserId();

            #region--modified part has been added to fix the issue with passing incorrect value of userId: it has to contain CustomerId but not UserName

            if (userId.Contains("CommerceUsers"))
            {
                GetUserRequest getUserRequest = new GetUserRequest(userId);

                GetUserResult result = customerServiceProvider.GetUser(getUserRequest);

                userId = result.CommerceUser.ExternalId;
            }

            #endregion

            GetCartsRequest getCartsRequest = new GetCartsRequest(shopContext.Name) { UserIds = new[] { userId } };
            IEnumerable<Cart> carts = this.CartServiceProvider.GetCarts(getCartsRequest).Carts.Select(cartBase => this.CartServiceProvider.LoadCart(new LoadCartRequest(shopContext.Name, cartBase.ExternalId, userId)).Cart);

            var conditionOperator = this.GetOperator();
            var isConditionMet = false;

            switch (conditionOperator)
            {
                case ConditionOperator.Equal:
                    isConditionMet = this.GetCartMetrics(carts).CompareTo(this.GetPredefinedValue()) == 0;
                    break;
                case ConditionOperator.GreaterThan:
                    isConditionMet = this.GetCartMetrics(carts).CompareTo(this.GetPredefinedValue()) > 0;
                    break;
                case ConditionOperator.GreaterThanOrEqual:
                    isConditionMet = this.GetCartMetrics(carts).CompareTo(this.GetPredefinedValue()) >= 0;
                    break;
                case ConditionOperator.LessThan:
                    isConditionMet = this.GetCartMetrics(carts).CompareTo(this.GetPredefinedValue()) < 0;
                    break;
                case ConditionOperator.LessThanOrEqual:
                    isConditionMet = this.GetCartMetrics(carts).CompareTo(this.GetPredefinedValue()) <= 0;
                    break;
                case ConditionOperator.NotEqual:
                    isConditionMet = this.GetCartMetrics(carts).CompareTo(this.GetPredefinedValue()) != 0;
                    break;
                default:
                    throw new InvalidOperationException("Operator is not supported.");
            }

            Log.Debug(string.Format(CultureInfo.InvariantCulture, "Connect cart condition: userId:{0}, Condition operator: {1}, ConditionMet: {2}, Number of carts found: {3}, Type: {4}", userId, conditionOperator, isConditionMet, (carts != null) ? carts.Count() : 0, this.GetType()));

            return isConditionMet;
        }

        
        protected override IComparable GetCartMetrics(IEnumerable<Cart> carts)
        {
            return carts.Where(cart => cart != null).Aggregate(0M, (productsCount, cart) => productsCount + cart.Lines.Where(cartLine => (cartLine != null) && (cartLine.Product != null)).GroupBy(cartLine => cartLine.Product.ProductId).Aggregate(0M, (partialProductCount, productGroup) => partialProductCount + productGroup.Aggregate(0M, (c, cartLine) => c + cartLine.Quantity)));
        }

        protected override object GetPredefinedValue()
        {
            return this.TotalProductQuantity;
        }

        
        public decimal TotalProductQuantity { get; set; }


    }
}
