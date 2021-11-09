export default {
    name: 'home',
    template: /*html*/`
    <b-container class="mt-3" fluid>
        <b-jumbotron header="NW Market - Orofena" lead="View market listings and more to come!">
        </b-jumbotron>
        <b-form-group
        >
            <b-input-group size="sm">
                <b-form-input
                    id="filter-input"
                    v-model="filter"
                    type="search"
                    placeholder="Type to Search"
                ></b-form-input>

                <b-input-group-append>
                    <b-button :disabled="!filter" @click="filter = ''">Clear</b-button>
                </b-input-group-append>
            </b-input-group>
        </b-form-group>
        <b-table striped hover borderless :items="marketData.Listings" :fields="listingFields" :filter="filter"></b-table>
    </b-container>
    `,
    data() {
        return {
            marketData: {
                Listings: []
            },
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
                }
            ],
            filter: null
        };
    },
    beforeMount() {
        this.getMarketData();
    },
    methods: {
        getMarketData() {
            fetch("https://nwmarketdata.s3.us-east-2.amazonaws.com/database.json")
            .then(response => response.json())
            .then(data => {
                this.marketData = data;
            });
        }
    }
};