export default {
    name: 'home',
    template: /*html*/`
    <b-container class="mt-3" fluid>
        <b-jumbotron header="NW Market - Orofena" lead="View market listings and more to come!">
        </b-jumbotron>
        <b-card no-body>
            <b-tabs card>
                <b-tab title="Listings" active>
                    <b-form-group
                    >
                        <b-input-group size="sm">
                            <b-form-input
                                id="filter-input"
                                v-model="filter"
                                type="search"
                                placeholder="Type to Search"
                                debounce="500"
                            ></b-form-input>

                            <b-input-group-append>
                                <b-button :disabled="!filter" @click="filter = ''">Clear</b-button>
                            </b-input-group-append>
                        </b-input-group>
                    </b-form-group>
                    <b-table striped hover borderless :items="marketData.Listings" :fields="listingFields" :filter="filter"></b-table>
                </b-tab>
                <b-tab title="Recipes">
                    <b-form-group
                    >
                        <b-input-group size="sm">
                            <b-form-input
                                id="filter-input"
                                v-model="filter"
                                type="search"
                                placeholder="Type to Search"
                                debounce="500"
                            ></b-form-input>

                            <b-input-group-append>
                                <b-button :disabled="!filter" @click="filter = ''">Clear</b-button>
                            </b-input-group-append>
                        </b-input-group>
                    </b-form-group>
                    <b-table striped hover borderless :items="recipeSuggestions" :fields="recipeSuggestionFields" :filter="filter"></b-table>
                </b-tab>
            </b-tabs>
        </b-card>
    </b-container>
    `,
    data() {
        return {
            marketData: {
                Listings: [],
            },
            recipeSuggestions: [],
            listingFields: [
                {
                    key: 'Name',
                    sortable: true,
                },
                {
                    key: 'Price',
                    sortable: true,
                },
                {
                    key: 'Location',
                    sortable: true,
                },
                {
                    key: 'available',
                    sortable: true,
                    sortByFormatted: true,
                    formatter: (value, key, item) => {
                        return item != null ? item.Instances[0].Available : null;
                    }
                },
                {
                    key: 'expires',
                    formatter: (value, key, item) => {
                        let hoursRemaining = this.getHoursFromTimeSpan(item.Instances[0].TimeRemaining);
                        return item != null ? moment(item.Instances[0].Time).add(hoursRemaining, 'h').fromNow() : null;
                    }
                },
                {
                    key: 'lastUpdated',
                    formatter: (value, key, item) => {
                        return item != null ? moment(item.Instances[0].Time).fromNow() : null;
                    }
                }
            ],
            recipeSuggestionFields: [
                {
                    key: 'Name',
                    sortable: true,
                },
                {
                    key: 'Tradeskill',
                    sortable: true,
                },
                {
                    key: 'LevelRequirement',
                    label: 'Level',
                    sortable: true,
                },
                {
                    key: 'CostPerQuantity',
                    label: 'Cost (ea)',
                    sortable: true,
                    formatter: (value, key, item) => {
                        return '$' + Math.round(value * 100) / 100;
                    }
                },
                {
                    key: 'ExperienceEfficienyForPrimaryTradekill',
                    label: 'Efficiency (xp/gp)',
                    sortable: true,
                    formatter: (value, key, item) => {
                        return Math.round(value * 100) / 100;
                    }
                },
                {
                    key: 'Crafts',
                },
                {
                    key: 'Buys',
                },
            ],
            filter: null
        };
    },
    beforeMount() {
        this.getMarketData();
        this.getRecipeSuggestions();
    },
    methods: {
        getMarketData() {
            fetch("https://nwmarketdata.s3.us-east-2.amazonaws.com/database.json")
            .then(response => response.json())
            .then(data => {
                this.marketData = data;
            });
        },
        getRecipeSuggestions() {
            fetch("https://nwmarketdata.s3.us-east-2.amazonaws.com/recipeSuggestions.json")
            .then(response => response.json())
            .then(data => {
                this.recipeSuggestions = data;
            });
        },
        getHoursFromTimeSpan(timeSpan) {
            if(!timeSpan) {
                return timeSpan;
            }

            let parts = timeSpan.split('.');

            if (parts.length < 1 || parts.length > 2) {
                return timeSpan;
            }

            let smallParts = parts[parts.length - 1].split(':');

            if (smallParts.length != 3) {
                return timeSpan;
            }

            return parseInt(parts[0]) * 24 + parseInt(smallParts[0]);
        }
    }
};